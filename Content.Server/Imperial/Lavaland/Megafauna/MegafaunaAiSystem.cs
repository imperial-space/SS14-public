using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Server.Imperial.Lavaland.MegafaunaSleep;
using Content.Shared.Atmos.Components;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Megafauna;

/// <summary>
/// Общая часть мегафауны SS13 и инструменты для способностей боссов: тайлы, задержки
/// (SLEEP_CHECK_DEATH), урон по клетке, тряска камеры.
/// </summary>
public sealed class MegafaunaAiSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly Content.Shared.Weapons.Ranged.Systems.SharedGunSystem _gun = default!;

    private readonly List<(TimeSpan Time, EntityUid? Owner, Action Action)> _scheduled = new();
    private readonly HashSet<EntityUid> _tileEnts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MegafaunaAiComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<MegafaunaAiComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<MegafaunaAiComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnRefreshSpeed(Entity<MegafaunaAiComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var mult = ent.Comp.Immobile ? 0f : ent.Comp.SpeedMultiplier;
        args.ModifySpeed(mult, mult);
    }

    private void OnBeforeDamage(Entity<MegafaunaAiComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (ent.Comp.Invulnerable && args.Damage.GetTotal() > 0)
            args.Cancelled = true;
    }

    private void OnMobStateChanged(Entity<MegafaunaAiComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        ent.Comp.Busy = false;
        ent.Comp.Immobile = false;
        ent.Comp.Invulnerable = false;
        if (ent.Comp.DeathSound != null)
            _audio.PlayPvs(ent.Comp.DeathSound, ent);
        if (ent.Comp.DeathMessage is { } msg)
            _popup.PopupEntity(Loc.GetString(msg, ("boss", ent.Owner)), ent, PopupType.LargeCaution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        if (_scheduled.Count > 0)
        {
            var due = _scheduled.Where(s => s.Time <= now).ToList();
            _scheduled.RemoveAll(s => s.Time <= now);
            foreach (var (_, owner, action) in due)
            {
                if (owner != null && !IsAliveBoss(owner.Value))
                    continue;
                action();
            }
        }

        var query = EntityQueryEnumerator<MegafaunaAiComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive || HasComp<LavalandMegafaunaSleepComponent>(uid))
                continue;

            if (now < comp.NextOpenFire)
                continue;
            comp.NextOpenFire = now + TimeSpan.FromSeconds(comp.OpenFireInterval);

            var oldTarget = comp.Target;
            comp.Target = FindTarget(uid, comp);
            if (oldTarget != comp.Target)
            {
                var changed = new MegafaunaTargetChangedEvent(oldTarget, comp.Target);
                RaiseLocalEvent(uid, ref changed);
            }

            if (comp.Target is not { } target)
                continue;

            var dist = TileDistance(uid, target);
            if (dist <= 1 && _mobState.IsDead(target))
            {
                if (comp.Devour)
                    Devour(uid, target, comp);
                continue;
            }

            // MoveToTarget: OpenFire только если цель не вплотную.
            if (dist <= 1)
                continue;

            var ev = new MegafaunaOpenFireEvent(target, dist);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    #region Targeting

    private EntityUid? FindTarget(EntityUid uid, MegafaunaAiComponent comp)
    {
        if (comp.Target is { } current && IsValidTarget(uid, current, comp.AggroRange))
            return current;

        EntityUid? best = null;
        var bestDist = float.MaxValue;
        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        var ourXform = Transform(uid);
        var ourPos = _transform.GetWorldPosition(ourXform);
        while (query.MoveNext(out var candidate, out _, out _, out var xform))
        {
            if (candidate == uid || xform.MapID != ourXform.MapID)
                continue;
            if (!IsValidTarget(uid, candidate, comp.AggroRange))
                continue;

            var d = (_transform.GetWorldPosition(xform) - ourPos).Length();
            if (d >= bestDist)
                continue;
            bestDist = d;
            best = candidate;
        }

        return best;
    }

    private bool IsValidTarget(EntityUid uid, EntityUid target, float range)
    {
        if (TerminatingOrDeleted(target) || HasComp<GhostComponent>(target) || HasComp<MegafaunaGuttedComponent>(target))
            return false;
        if (!HasComp<MobStateComponent>(target) || HasComp<MegafaunaAiComponent>(target))
            return false;

        var a = Transform(uid);
        var b = Transform(target);
        if (a.MapID != b.MapID)
            return false;

        return (_transform.GetWorldPosition(a) - _transform.GetWorldPosition(b)).Length() <= range;
    }

    /// <summary>devour: лечит на половину здоровья жертвы и потрошит её.</summary>
    public void Devour(EntityUid uid, EntityUid victim, MegafaunaAiComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        _popup.PopupEntity(Loc.GetString(comp.DevourMessage, ("boss", uid), ("victim", victim)), uid, PopupType.LargeCaution);
        var ev = new MegafaunaDevourEvent(victim);
        RaiseLocalEvent(uid, ref ev);

        if (_thresholds.TryGetThresholdForState(victim, MobState.Dead, out var victimMax))
            Heal(uid, victimMax.Value.Float() * 0.5f);

        Damage(victim, "Blunt", 500f, uid);
        EnsureComp<MegafaunaGuttedComponent>(victim);
        comp.Target = null;
    }

    public EntityUid? GetTarget(EntityUid uid)
    {
        return TryComp<MegafaunaAiComponent>(uid, out var comp) ? comp.Target : null;
    }

    #endregion

    #region Abilities

    public bool IsAliveBoss(EntityUid uid)
    {
        return !TerminatingOrDeleted(uid) && _mobState.IsAlive(uid);
    }

    /// <summary>SLEEP_CHECK_DEATH: действие выполнится через задержку, только если владелец ещё жив.</summary>
    public void Schedule(EntityUid? owner, float seconds, Action action)
    {
        if (seconds <= 0f && (owner == null || IsAliveBoss(owner.Value)))
        {
            action();
            return;
        }

        _scheduled.Add((_timing.CurTime + TimeSpan.FromSeconds(seconds), owner, action));
    }

    /// <summary>Trigger: способность доступна, если не идёт другая атака и общий откат прошёл.</summary>
    public bool CanUseAbility(EntityUid uid, MegafaunaAiComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;
        return !comp.Busy && _timing.CurTime >= comp.NextAbility;
    }

    /// <summary>disable_cooldown_actions.</summary>
    public bool TryBeginAbility(EntityUid uid, MegafaunaAiComponent? comp = null)
    {
        if (!Resolve(uid, ref comp) || !CanUseAbility(uid, comp))
            return false;
        comp.Busy = true;
        return true;
    }

    /// <summary>StartCooldown + enable_cooldown_actions: откат общий для всех способностей.</summary>
    public void EndAbility(EntityUid uid, float cooldown, MegafaunaAiComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;
        comp.Busy = false;
        comp.NextAbility = _timing.CurTime + TimeSpan.FromSeconds(cooldown);
    }

    /// <summary>StartCooldown(0): сбросить общий откат.</summary>
    public void ResetCooldown(EntityUid uid, MegafaunaAiComponent? comp = null)
    {
        if (Resolve(uid, ref comp, false))
            comp.NextAbility = _timing.CurTime;
    }

    public void SetImmobile(EntityUid uid, bool immobile, MegafaunaAiComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;
        comp.Immobile = immobile;
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    public void SetSpeedMultiplier(EntityUid uid, float mult, MegafaunaAiComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;
        comp.SpeedMultiplier = mult;
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    public float GetMaxHealth(EntityUid uid)
    {
        return _thresholds.TryGetThresholdForState(uid, MobState.Dead, out var max) ? max.Value.Float() : 1f;
    }

    public float GetHealth(EntityUid uid)
    {
        return GetMaxHealth(uid) - _damageable.GetTotalDamage(uid).Float();
    }

    /// <summary>anger_modifier = clamp((maxHealth - health) / divisor, 0, max).</summary>
    public float Anger(EntityUid uid, float divisor, float max)
    {
        return Math.Clamp((GetMaxHealth(uid) - GetHealth(uid)) / divisor, 0f, max);
    }

    public bool Prob(float percent)
    {
        return _random.Prob(Math.Clamp(percent / 100f, 0f, 1f));
    }

    #endregion

    #region Damage / effects

    public void Damage(EntityUid target, string type, float amount, EntityUid? origin = null)
    {
        if (amount <= 0)
            return;
        var spec = new DamageSpecifier();
        spec.DamageDict.Add(type, FixedPoint2.New(amount));
        _damageable.TryChangeDamage(target, spec, origin: origin);
    }

    public void Heal(EntityUid uid, float amount)
    {
        if (amount > 0 && TryComp<DamageableComponent>(uid, out var damageable))
            _damageable.HealEvenly((uid, damageable), FixedPoint2.New(-amount));
    }

    /// <summary>fire_act: поджигает моба (3 огненных стака).</summary>
    public void Ignite(EntityUid target)
    {
        if (TryComp<FlammableComponent>(target, out var flammable))
            _flammable.AdjustFireStacks(target, 3f, flammable, ignite: true);
    }

    /// <summary>shake_camera всем игрокам в радиусе.</summary>
    public void ShakeCamera(EntityCoordinates center, float range, float strength)
    {
        var mapPos = _transform.ToMapCoordinates(center);
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var player, out _, out var xform))
        {
            if (xform.MapID != mapPos.MapId)
                continue;
            if ((_transform.GetWorldPosition(xform) - mapPos.Position).Length() > range)
                continue;
            var dir = new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f));
            _recoil.KickCamera(player, dir * strength);
        }
    }

    /// <summary>
    /// shoot_projectile: снаряд из клетки стрелка под углом byondAngle
    /// (градусы по часовой стрелке от севера, как в BYOND).
    /// </summary>
    public EntityUid ShootProjectile(EntityUid shooter, string proto, float byondAngle, float speed, EntityCoordinates? from = null)
    {
        var coords = from ?? Transform(shooter).Coordinates;
        var mathRad = (90f - byondAngle) * MathF.PI / 180f;
        var dir = new Vector2(MathF.Cos(mathRad), MathF.Sin(mathRad));
        var projectile = Spawn(proto, coords);
        _gun.ShootProjectile(projectile, dir, Vector2.Zero, shooter, shooter, speed);
        return projectile;
    }

    /// <summary>get_angle в BYOND-градусах (по часовой от севера).</summary>
    public float ByondAngle(EntityCoordinates from, EntityCoordinates to)
    {
        var a = _transform.ToMapCoordinates(from).Position;
        var b = _transform.ToMapCoordinates(to).Position;
        var d = b - a;
        return 90f - MathF.Atan2(d.Y, d.X) * 180f / MathF.PI;
    }

    public void PlaySound(SoundSpecifier sound, EntityCoordinates coords, float volume = 0f)
    {
        _audio.PlayPvs(sound, coords, AudioParams.Default.WithVolume(volume));
    }

    #endregion

    #region Tiles

    public bool TryGetTile(EntityUid uid, out Entity<MapGridComponent> grid, out Vector2i tile)
    {
        return TryGetTile(Transform(uid).Coordinates, out grid, out tile);
    }

    public bool TryGetTile(EntityCoordinates coords, out Entity<MapGridComponent> grid, out Vector2i tile)
    {
        grid = default;
        tile = default;
        var gridUid = _transform.GetGrid(coords);
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;

        grid = (gridUid.Value, gridComp);
        tile = _map.CoordinatesToTile(gridUid.Value, gridComp, coords);
        return true;
    }

    public EntityCoordinates TileCenter(Entity<MapGridComponent> grid, Vector2i tile)
    {
        return _map.GridTileToLocal(grid, grid.Comp, tile);
    }

    /// <summary>get_dist в клетках (Чебышёв).</summary>
    public int TileDistance(EntityUid a, EntityUid b)
    {
        if (!TryGetTile(a, out var gridA, out var tileA) || !TryGetTile(b, out var gridB, out var tileB) || gridA.Owner != gridB.Owner)
        {
            var pa = _transform.GetWorldPosition(a);
            var pb = _transform.GetWorldPosition(b);
            return (int) MathF.Round(MathF.Max(MathF.Abs(pa.X - pb.X), MathF.Abs(pa.Y - pb.Y)));
        }

        return Chebyshev(tileA, tileB);
    }

    public static int Chebyshev(Vector2i a, Vector2i b)
    {
        return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    /// <summary>RANGE_TURFS: квадрат клеток радиусом range.</summary>
    public static IEnumerable<Vector2i> RangeTiles(Vector2i center, int range)
    {
        for (var x = -range; x <= range; x++)
        for (var y = -range; y <= range; y++)
            yield return center + new Vector2i(x, y);
    }

    /// <summary>get_line: клетки отрезка (Брезенхем), включая концы.</summary>
    public static List<Vector2i> Line(Vector2i from, Vector2i to)
    {
        var result = new List<Vector2i>();
        int x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y;
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;
        while (true)
        {
            result.Add(new Vector2i(x0, y0));
            if (x0 == x1 && y0 == y1)
                break;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }

        return result;
    }

    /// <summary>Клетка в направлении angle (градусы, 0 = восток, против часовой) на distance клеток.</summary>
    public static Vector2i TileAtAngle(Vector2i origin, float angleDeg, float distance)
    {
        var rad = angleDeg * MathF.PI / 180f;
        return origin + new Vector2i((int) MathF.Round(MathF.Cos(rad) * distance), (int) MathF.Round(MathF.Sin(rad) * distance));
    }

    /// <summary>Угол от a к b в градусах (0 = восток, против часовой).</summary>
    public static float AngleTo(Vector2i a, Vector2i b)
    {
        return MathF.Atan2(b.Y - a.Y, b.X - a.X) * 180f / MathF.PI;
    }

    /// <summary>get_cardinal_dir / get_dir в виде единичного шага.</summary>
    public static Vector2i StepTowards(Vector2i from, Vector2i to)
    {
        return new Vector2i(Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
    }

    /// <summary>is_blocked_turf: стена или плотный объект (мобы не считаются).</summary>
    public bool IsBlocked(Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(grid, grid.Comp, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return true;

        _tileEnts.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid, tile, _tileEnts, flags: LookupFlags.Static | LookupFlags.Sundries);
        foreach (var ent in _tileEnts)
        {
            if (HasComp<MobStateComponent>(ent))
                continue;
            if (!TryComp<Robust.Shared.Physics.Components.PhysicsComponent>(ent, out var physics) || !physics.Hard || !physics.CanCollide)
                continue;
            if ((physics.CollisionLayer & (int) CollisionGroup.Impassable) != 0)
                return true;
        }

        return false;
    }

    public bool IsMineral(Entity<MapGridComponent> grid, Vector2i tile)
    {
        _tileEnts.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid, tile, _tileEnts, flags: LookupFlags.Static);
        foreach (var ent in _tileEnts)
        {
            if (HasComp<GatherableComponent>(ent) && Transform(ent).Anchored)
                return true;
        }

        return false;
    }

    /// <summary>gets_drilled: срыть породу на клетке (руда выпадает).</summary>
    public void Drill(Entity<MapGridComponent> grid, Vector2i tile, EntityUid? driller = null)
    {
        _tileEnts.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid, tile, _tileEnts, flags: LookupFlags.Static);
        foreach (var ent in _tileEnts.ToArray())
        {
            if (HasComp<GatherableComponent>(ent) && Transform(ent).Anchored)
                _gatherable.Gather(ent, driller);
        }
    }

    /// <summary>Живые и мёртвые мобы на клетке.</summary>
    public List<EntityUid> MobsOnTile(Entity<MapGridComponent> grid, Vector2i tile)
    {
        _tileEnts.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid, tile, _tileEnts, 0f, LookupFlags.Dynamic | LookupFlags.Sundries);
        return _tileEnts.Where(HasComp<MobStateComponent>).ToList();
    }

    public EntityUid SpawnAt(string proto, Entity<MapGridComponent> grid, Vector2i tile)
    {
        return Spawn(proto, TileCenter(grid, tile));
    }

    #endregion
}
