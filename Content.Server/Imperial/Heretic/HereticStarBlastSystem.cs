using System.Numerics;
using Content.Server.Imperial.Heretic.Components;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticStarBlastSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem    _lookup        = default!;
    [Dependency] private readonly PopupSystem           _popup         = default!;
    [Dependency] private readonly SharedAudioSystem     _audio         = default!;
    [Dependency] private readonly SharedPhysicsSystem   _physics       = default!;
    [Dependency] private readonly SharedStunSystem      _stun          = default!;
    [Dependency] private readonly SharedTransformSystem _xform         = default!;
    [Dependency] private readonly SharedActionsSystem   _actions       = default!;
    [Dependency] private readonly StatusEffectsSystem   _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, HereticStarBlastActionEvent>(OnStarBlast);
        SubscribeLocalEvent<HereticStarBlastProjectileComponent, StartCollideEvent>(OnProjectileCollide);
        SubscribeLocalEvent<HereticStarBlastProjectileComponent, EntityTerminatingEvent>(OnProjectileTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticStarBlastProjectileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            comp.TrailTimer += frameTime;
            if (comp.TrailTimer < comp.TrailInterval)
                continue;
            comp.TrailTimer = 0f;
            var localPos = xform.LocalPosition;
            var snapped = new Vector2(MathF.Floor(localPos.X) + 0.5f, MathF.Floor(localPos.Y) + 0.5f);
            var trailLevel = comp.Shooter != EntityUid.Invalid && TryComp<HereticComponent>(comp.Shooter, out var trailH)
                ? trailH.PassiveLevel : 0;
            SpawnCarpet(trailLevel, xform.ParentUid, snapped);
        }
    }

    private void OnStarBlast(EntityUid uid, HereticComponent comp, HereticStarBlastActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var starBlast = EnsureComp<HereticStarBlastComponent>(uid);

        if (starBlast.ActiveProjectile != null && !TerminatingOrDeleted(starBlast.ActiveProjectile.Value))
            Detonate(uid, starBlast, args);
        else
            Shoot(uid, starBlast, args);
    }

    private void Shoot(EntityUid uid, HereticStarBlastComponent starBlast, HereticStarBlastActionEvent args)
    {
        var originCoords = Transform(uid).Coordinates;
        var originWorldPos = _xform.GetWorldPosition(uid);
        var targetWorldPos = _xform.ToMapCoordinates(args.Target).Position;

        var direction = targetWorldPos - originWorldPos;
        if (direction.LengthSquared() < 0.001f)
            direction = new Vector2(1f, 0f);
        direction = Vector2.Normalize(direction);

        var projectile = Spawn("ProjectileStarBlast", originCoords);
        var projComp = EnsureComp<HereticStarBlastProjectileComponent>(projectile);
        projComp.Shooter = uid;

        if (TryComp<PhysicsComponent>(projectile, out var physics))
            _physics.SetLinearVelocity(projectile, direction * projComp.Speed, body: physics);

        starBlast.ActiveProjectile = projectile;
        starBlast.StarBlastAction = args.Action.Owner;

        // SetUseDelay изменяет компонент ДО того как система вызовет StartUseDelay(UseDelay)
        _actions.SetUseDelay(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp), TimeSpan.FromSeconds(0.5));
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_cosmic_energy.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-star-blast-fired"), uid, uid, PopupType.Medium);
    }

    private void Detonate(EntityUid uid, HereticStarBlastComponent starBlast, HereticStarBlastActionEvent args)
    {
        var projUid = starBlast.ActiveProjectile!.Value;
        var projCoords = Transform(projUid).Coordinates;
        var hereticCoords = Transform(uid).Coordinates;

        // SS13: pull_victims() от текущей позиции → телепорт → pull_victims() от новой позиции
        PullVictims(uid, hereticCoords);

        _xform.SetCoordinates(uid, projCoords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_cosmic_energy.ogg"), uid);

        PullVictims(uid, projCoords);

        QueueDel(projUid);

        _popup.PopupEntity(Loc.GetString("heretic-star-blast-detonated"), uid, uid, PopupType.Large);
    }

    private void PullVictims(EntityUid hereticUid, EntityCoordinates coords)
    {
        var localPos = coords.Position;
        var snapped = new Vector2(MathF.Floor(localPos.X) + 0.5f, MathF.Floor(localPos.Y) + 0.5f);
        var parentUid = coords.EntityId;

        var pullLevel = TryComp<HereticComponent>(hereticUid, out var pullH) ? pullH.PassiveLevel : 0;

        // SS13 range(1) = 3x3 square
        for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                SpawnCarpet(pullLevel, parentUid, snapped + new Vector2(dx, dy));

        var hereticWorldPos = _xform.GetWorldPosition(hereticUid);

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 2f))
        {
            if (ent.Owner == hereticUid) continue;

            _statusEffects.TrySetStatusEffectDuration(ent.Owner, "StarMarkStatusEffect", TimeSpan.FromSeconds(30));

            if (!TryComp<PhysicsComponent>(ent.Owner, out var mobPhysics)) continue;

            var mobPos = _xform.GetWorldPosition(ent.Owner);
            var delta = hereticWorldPos - mobPos;
            if (delta.LengthSquared() > 0.01f)
                _physics.SetLinearVelocity(ent.Owner, Vector2.Normalize(delta) * 6f, body: mobPhysics);
        }
    }

    private void OnProjectileCollide(EntityUid uid, HereticStarBlastProjectileComponent comp, ref StartCollideEvent args)
    {
        switch (args.OurFixtureId)
        {
            case "mob_sensor":
                if (args.OtherEntity == comp.Shooter) return;
                if (!HasComp<MobStateComponent>(args.OtherEntity)) return;

                var coords = Transform(uid).Coordinates;
                _stun.TryKnockdown(args.OtherEntity, TimeSpan.FromSeconds(4), true);

                foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 3f))
                {
                    if (ent.Owner == comp.Shooter) continue;
                    _statusEffects.TrySetStatusEffectDuration(ent.Owner, "StarMarkStatusEffect", TimeSpan.FromSeconds(30));
                }

                QueueDel(uid);
                break;

            case "wall_stop":
                QueueDel(uid);
                break;
        }
    }

    private void SpawnCarpet(int passiveLevel, EntityUid parent, Vector2 pos)
    {
        var carpet = Spawn("HereticCosmicCarpet", new EntityCoordinates(parent, pos));
        if (passiveLevel > 0 && TryComp<HereticCosmicFieldComponent>(carpet, out var field))
            field.PassiveLevel = passiveLevel;
    }

    private void OnProjectileTerminating(EntityUid uid, HereticStarBlastProjectileComponent comp, ref EntityTerminatingEvent args)
    {
        if (!TryComp<HereticStarBlastComponent>(comp.Shooter, out var starBlast)) return;
        if (starBlast.ActiveProjectile != uid)
            return;

        starBlast.ActiveProjectile = null;

        if (starBlast.StarBlastAction != null)
        {
            var actionEnt = new Entity<ActionComponent?>(starBlast.StarBlastAction.Value, null);
            _actions.SetUseDelay(actionEnt, TimeSpan.FromSeconds(25));
            _actions.SetCooldown(actionEnt, TimeSpan.FromSeconds(25));
        }
    }
}
