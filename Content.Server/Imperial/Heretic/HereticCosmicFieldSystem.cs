using System.Numerics;
using Content.Server.Imperial.Heretic.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticCosmicFieldSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem           _lookup   = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movSpeed = default!;
    [Dependency] private readonly SharedPhysicsSystem         _physics  = default!;
    [Dependency] private readonly SharedTransformSystem       _xform    = default!;
    [Dependency] private readonly IGameTiming                 _timing   = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _pushImmunity  = new();
    private readonly HashSet<EntityUid>               _slowedByField = new();

    private TimeSpan _nextProximityCheck;
    private static readonly TimeSpan ProximityCheckInterval = TimeSpan.FromSeconds(0.25);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticCosmicFieldComponent, StartCollideEvent>(OnEnter);
        SubscribeLocalEvent<HereticCosmicFieldComponent, EndCollideEvent>(OnExit);
        SubscribeLocalEvent<HereticCosmicFieldComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CosmicFieldBoostedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshBoosted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Чистим истёкшие записи иммунитета к отталкиванию
        var now = _timing.CurTime;
        var expired = new List<EntityUid>();
        foreach (var (ent, until) in _pushImmunity)
        {
            if (until <= now)
                expired.Add(ent);
        }
        foreach (var key in expired)
            _pushImmunity.Remove(key);

        // Чистим удалённые снаряды из set'а замедленных
        _slowedByField.RemoveWhere(e => TerminatingOrDeleted(e));

        var doProximityCheck = now >= _nextProximityCheck;
        if (doProximityCheck)
            _nextProximityCheck = now + ProximityCheckInterval;

        // Регенерация выносливости для еретика Пути Космоса, стоящего в поле
        var query = EntityQueryEnumerator<HereticCosmicFieldComponent>();
        while (query.MoveNext(out var fieldUid, out var field))
        {
            foreach (var ent in field.BoostedMobs)
            {
                if (TerminatingOrDeleted(ent))
                    continue;

                if (!TryComp<HereticComponent>(ent, out var heretic))
                    continue;

                if (heretic.CurrentPath != HereticPath.Cosmos)
                    continue;

                if (!TryComp<StaminaComponent>(ent, out var stamina))
                    continue;

                if (stamina.StaminaDamage <= 0f)
                    continue;

                stamina.StaminaDamage = MathF.Max(0f, stamina.StaminaDamage - 15f * frameTime);
                Dirty(ent, stamina);
            }

            // Периодически убираем бусты у еретиков, которые уже вышли из поля.
            // EndCollideEvent не всегда срабатывает надёжно для статичных тел.
            if (!doProximityCheck || field.BoostedMobs.Count == 0)
                continue;

            var mapCoords = _xform.GetMapCoordinates(fieldUid);
            var inRangeSet = new HashSet<EntityUid>();
            foreach (var mob in _lookup.GetEntitiesInRange<HereticComponent>(mapCoords, 0.6f))
                inRangeSet.Add(mob);

            var toRemove = new List<EntityUid>();
            foreach (var mob in field.BoostedMobs)
            {
                if (TerminatingOrDeleted(mob) || !inRangeSet.Contains(mob))
                    toRemove.Add(mob);
            }

            foreach (var mob in toRemove)
            {
                field.BoostedMobs.Remove(mob);
                if (!TryComp<CosmicFieldBoostedComponent>(mob, out var boost))
                    continue;
                boost.FieldCount = Math.Max(0, boost.FieldCount - 1);
                if (boost.FieldCount == 0)
                    RemComp<CosmicFieldBoostedComponent>(mob);
                if (!TerminatingOrDeleted(mob))
                    _movSpeed.RefreshMovementSpeedModifiers(mob);
            }
        }
    }

    private void OnEnter(EntityUid uid, HereticCosmicFieldComponent comp, ref StartCollideEvent args)
    {
        var other = args.OtherEntity;

        // Уровень 3: замедлить снаряд, влетевший в поле (0.2× скорость)
        if (comp.PassiveLevel >= 3 && HasComp<ProjectileComponent>(other))
        {
            if (!_slowedByField.Contains(other) && TryComp<PhysicsComponent>(other, out var projPhys))
            {
                _slowedByField.Add(other);
                _physics.SetLinearVelocity(other, projPhys.LinearVelocity * 0.2f, body: projPhys);
            }
            return;
        }

        if (!HasComp<MobStateComponent>(other))
            return;

        if (HasComp<StarMarkComponent>(other) && !HasComp<HereticComponent>(other))
        {
            // Блокировка входа: отталкиваем помеченного не-еретика за границу поля
            if (!_pushImmunity.TryGetValue(other, out var immuneUntil) || immuneUntil <= _timing.CurTime)
            {
                PushMobOutside(uid, other);
                _pushImmunity[other] = _timing.CurTime + TimeSpan.FromSeconds(0.5);
            }
            return;
        }

        if (!HasComp<HereticComponent>(other))
            return;

        if (!comp.BoostedMobs.Add(other))
            return;

        var boost = EnsureComp<CosmicFieldBoostedComponent>(other);
        boost.FieldCount++;
        _movSpeed.RefreshMovementSpeedModifiers(other);
    }

    private void OnExit(EntityUid uid, HereticCosmicFieldComponent comp, ref EndCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!comp.BoostedMobs.Remove(other))
            return;

        if (!TryComp<CosmicFieldBoostedComponent>(other, out var boost))
            return;

        boost.FieldCount = Math.Max(0, boost.FieldCount - 1);
        if (boost.FieldCount == 0)
            RemComp<CosmicFieldBoostedComponent>(other);
        _movSpeed.RefreshMovementSpeedModifiers(other);
    }

    private void OnShutdown(EntityUid uid, HereticCosmicFieldComponent comp, ComponentShutdown args)
    {
        foreach (var ent in comp.BoostedMobs)
        {
            if (!TryComp<CosmicFieldBoostedComponent>(ent, out var boost))
                continue;

            boost.FieldCount = Math.Max(0, boost.FieldCount - 1);
            if (boost.FieldCount == 0)
                RemComp<CosmicFieldBoostedComponent>(ent);
            _movSpeed.RefreshMovementSpeedModifiers(ent);
        }
        comp.BoostedMobs.Clear();
    }

    // Телепортируем моба чуть за границу ковра (0.6 тайла от центра = вне сенсора 0.49)
    private void PushMobOutside(EntityUid carpetUid, EntityUid mobUid)
    {
        var carpetXform = Transform(carpetUid);
        var mobXform    = Transform(mobUid);

        if (carpetXform.ParentUid != mobXform.ParentUid)
            return;

        var carpetPos = carpetXform.LocalPosition;
        var mobPos    = mobXform.LocalPosition;
        var diff      = mobPos - carpetPos;

        if (diff.LengthSquared() < 0.001f)
            diff = new Vector2(0.6f, 0f);
        else
            diff = Vector2.Normalize(diff) * 0.6f;

        _xform.SetCoordinates(mobUid, new EntityCoordinates(carpetXform.ParentUid, carpetPos + diff));
    }

    private void OnRefreshBoosted(EntityUid uid, CosmicFieldBoostedComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(1.25f, 1.25f);
    }
}
