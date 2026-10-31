using System.Numerics;
using Content.Server.Imperial.ZLevels;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Вооружение шаттлов и высадка. Стрелок за постом видит борт сверху и ведёт пушки курсором: снаряды бьют по
/// кораблям на своём этаже, а из неба очередь ложится на землю под прицелом. Бомбоотсеки сбрасывают бомбы
/// на поверхность под бортом с упреждением по скорости. Парашют уносит с неба на землю, с него можно рулить.
/// </summary>
public sealed class CadiaShipWeaponSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CadiaPylonVeilSystem _veil = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedContentEyeSystem _contentEye = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly CadiaFlightSystem _flight = default!;
    [Dependency] private readonly Content.Server.Explosion.EntitySystems.ExplosionSystem _explosion = default!;

    private static readonly EntProtoId BombAction = "ActionCadiaShipBomb";
    private static readonly EntProtoId LeaveAction = "ActionCadiaShipLeaveGun";
    private static readonly EntProtoId SelectAction = "ActionCadiaShipSelectGun";
    private static readonly ProtoId<DamageTypePrototype> PiercingDamage = "Piercing";
    private static readonly ProtoId<DamageTypePrototype> StructuralDamage = "Structural";

    private static readonly SoundSpecifier EnterSound = new SoundPathSpecifier("/Audio/Machines/scanning.ogg");
    private static readonly SoundSpecifier SelectSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");
    private static readonly SoundSpecifier DryFireSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg");
    private static readonly SoundSpecifier BombReleaseSound = new SoundPathSpecifier("/Audio/Machines/door_lock_off.ogg");
    private static readonly SoundSpecifier LoadSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/batrifle_magin.ogg");
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
    private static readonly SoundSpecifier ChuteOpenSound = new SoundPathSpecifier("/Audio/Items/zip.ogg");
    private static readonly SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/bodyfall1.ogg");

    private const float GunnerReach = 1.8f;
    private const float AimTolerance = 12f;
    private const float StrafeRadius = 0.8f;
    private const float StrafeDelay = 0.35f;
    private const int SafeLandingSearch = 6;
    private static readonly TimeSpan CanopyOpenTime = TimeSpan.FromSeconds(0.36);
    private static readonly TimeSpan DirtyRate = TimeSpan.FromSeconds(0.1);

    private readonly List<(TimeSpan At, MapCoordinates Where, float Damage, float Explosion, EntityUid Source)> _strafes = new();
    private readonly HashSet<Entity<DamageableComponent>> _hit = new();
    private TimeSpan _nextDirty;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaShipGunConsoleComponent, ActivateInWorldEvent>(OnConsoleActivate);
        SubscribeLocalEvent<CadiaShipGunConsoleComponent, ExaminedEvent>(OnConsoleExamined);
        SubscribeLocalEvent<CadiaShipGunnerComponent, ComponentShutdown>(OnGunnerShutdown);
        SubscribeLocalEvent<CadiaShipGunnerComponent, CadiaShipLeaveGunActionEvent>(OnLeave);
        SubscribeLocalEvent<CadiaShipGunnerComponent, CadiaShipBombActionEvent>(OnBomb);
        SubscribeLocalEvent<CadiaShipGunnerComponent, CadiaShipSelectGunActionEvent>(OnSelectGun);
        SubscribeLocalEvent<CadiaShipGunComponent, InteractUsingEvent>(OnGunInteract);
        SubscribeLocalEvent<CadiaShipGunComponent, ExaminedEvent>(OnGunExamined);
        SubscribeLocalEvent<CadiaShipBombBayComponent, InteractUsingEvent>(OnBayInteract);
        SubscribeLocalEvent<CadiaShipBombBayComponent, ExaminedEvent>(OnBayExamined);
        SubscribeNetworkEvent<CadiaShipAimEvent>(OnAim);
        SubscribeNetworkEvent<CadiaShipFireEvent>(OnFire);

        SubscribeLocalEvent<CadiaParachuteComponent, CadiaParachuteJumpActionEvent>(OnJump);
        SubscribeLocalEvent<CadiaParachuteComponent, ExaminedEvent>(OnChuteExamined);
        SubscribeLocalEvent<ZBeforeFallEvent>(OnBeforeFall);
    }

    // ═══════════════════════ Пост стрелка ═══════════════════════

    private void OnConsoleActivate(Entity<CadiaShipGunConsoleComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = true;
        var user = args.User;
        if (Transform(ent).GridUid is not { } grid)
            return;
        if (ent.Comp.Gunner is { } busy && busy != user && HasComp<CadiaShipGunnerComponent>(busy))
        {
            _popup.PopupEntity(Loc.GetString("cadia-ship-console-busy"), ent, user);
            return;
        }
        if (HasComp<CadiaShipGunnerComponent>(user))
        {
            RemComp<CadiaShipGunnerComponent>(user);
            return;
        }

        var gunner = EnsureComp<CadiaShipGunnerComponent>(user);
        gunner.Console = ent;
        gunner.Grid = grid;
        gunner.HadFov = TryComp<EyeComponent>(user, out var eye) && eye.DrawFov;
        Dirty(user, gunner);
        ent.Comp.Gunner = user;
        Dirty(ent);

        _eye.SetTarget(user, grid);
        _eye.SetDrawFov(user, false);
        _contentEye.SetZoom(user, new Vector2(ent.Comp.Zoom), ignoreLimits: true);

        if (_actions.AddAction(user, SelectAction) is { } select)
            gunner.Actions.Add(select);
        if (_actions.AddAction(user, BombAction) is { } bomb)
            gunner.Actions.Add(bomb);
        if (_actions.AddAction(user, LeaveAction) is { } leave)
            gunner.Actions.Add(leave);
        _audio.PlayEntity(EnterSound, Filter.Entities(user), ent, false);
        _popup.PopupEntity(Loc.GetString("cadia-ship-gunner-enter"), user, user, PopupType.Medium);
    }

    private void OnGunnerShutdown(Entity<CadiaShipGunnerComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<CadiaShipGunConsoleComponent>(ent.Comp.Console, out var console) && console.Gunner == ent.Owner)
        {
            console.Gunner = null;
            Dirty(ent.Comp.Console, console);
        }
        if (TerminatingOrDeleted(ent))
            return;
        foreach (var action in ent.Comp.Actions)
            _actions.RemoveAction(ent.Owner, action);
        _eye.SetTarget(ent, null);
        _eye.SetDrawFov(ent, ent.Comp.HadFov);
        _contentEye.ResetZoom(ent);
    }

    private void OnLeave(Entity<CadiaShipGunnerComponent> ent, ref CadiaShipLeaveGunActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        RemComp<CadiaShipGunnerComponent>(ent);
    }

    /// <summary>Выбор пушки по кругу: все сразу, затем каждая отдельно.</summary>
    private void OnSelectGun(Entity<CadiaShipGunnerComponent> ent, ref CadiaShipSelectGunActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var guns = new List<EntityUid>();
        var query = EntityQueryEnumerator<CadiaShipGunComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == ent.Comp.Grid)
                guns.Add(uid);
        }
        guns.Sort();
        if (guns.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cadia-ship-no-guns"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var index = ent.Comp.Selected is { } current ? guns.IndexOf(current) : -1;
        ent.Comp.Selected = index + 1 < guns.Count ? guns[index + 1] : null;
        Dirty(ent);
        _audio.PlayEntity(SelectSound, Filter.Entities(ent.Owner), ent, false);
        _popup.PopupEntity(ent.Comp.Selected is { } gun
                ? Loc.GetString("cadia-ship-gun-selected", ("gun", Name(gun)), ("number", guns.IndexOf(gun) + 1), ("total", guns.Count))
                : Loc.GetString("cadia-ship-gun-selected-all", ("total", guns.Count)),
            ent, ent);
    }

    private void OnConsoleExamined(Entity<CadiaShipGunConsoleComponent> ent, ref ExaminedEvent args)
    {
        if (Transform(ent).GridUid is not { } grid)
            return;
        var guns = 0;
        var bays = 0;
        var gunQuery = EntityQueryEnumerator<CadiaShipGunComponent, TransformComponent>();
        while (gunQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == grid)
                guns++;
        }
        var bayQuery = EntityQueryEnumerator<CadiaShipBombBayComponent, TransformComponent>();
        while (bayQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == grid)
                bays++;
        }
        args.PushMarkup(Loc.GetString("cadia-ship-console-examine", ("guns", guns), ("bays", bays)));
    }

    private void OnAim(CadiaShipAimEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } user && TryComp<CadiaShipGunnerComponent>(user, out var gunner))
            gunner.Aim = _xform.ToMapCoordinates(GetCoordinates(ev.Target));
    }

    private void OnFire(CadiaShipFireEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } user && TryComp<CadiaShipGunnerComponent>(user, out var gunner))
            gunner.Firing = ev.Down && !_veil.Blocks(user, predicted: false);
    }

    // ═══════════════════════ Пушки ═══════════════════════

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var dirty = now >= _nextDirty;
        if (dirty)
            _nextDirty = now + DirtyRate;

        var gunners = EntityQueryEnumerator<CadiaShipGunnerComponent, TransformComponent>();
        var leaving = new List<EntityUid>();
        while (gunners.MoveNext(out var uid, out var gunner, out var xform))
        {
            // Отошёл от поста или пост снесли — стрельба кончилась.
            if (TerminatingOrDeleted(gunner.Console) || TerminatingOrDeleted(gunner.Grid) ||
                !xform.Coordinates.TryDistance(EntityManager, Transform(gunner.Console).Coordinates, out var distance) || distance > GunnerReach)
            {
                leaving.Add(uid);
                continue;
            }
            if (gunner.Aim.MapId == MapId.Nullspace)
                continue;
            DriveGuns(uid, gunner, frameTime, now, dirty);
        }
        foreach (var uid in leaving)
            RemComp<CadiaShipGunnerComponent>(uid);

        UpdateStrafes(now);
        UpdateChutes(now);
    }

    private void DriveGuns(EntityUid user, CadiaShipGunnerComponent gunner, float frameTime, TimeSpan now, bool dirty)
    {
        var guns = EntityQueryEnumerator<CadiaShipGunComponent, TransformComponent>();
        while (guns.MoveNext(out var uid, out var gun, out var xform))
        {
            if (xform.GridUid != gunner.Grid)
                continue;
            var origin = _xform.GetMapCoordinates(uid, xform);
            if (origin.MapId != gunner.Aim.MapId)
                continue;
            var delta = gunner.Aim.Position - origin.Position;
            if (delta.LengthSquared() < 0.05f)
                continue;

            var wanted = new Angle(MathF.Atan2(-delta.X, delta.Y));
            var diff = Angle.ShortestDistance(gun.Angle, wanted).Degrees;
            var step = gun.TurnRate * frameTime;
            if (Math.Abs(diff) > 0.01)
            {
                gun.Angle = Math.Abs(diff) <= step ? wanted : gun.Angle + Angle.FromDegrees(Math.Sign(diff) * step);
                if (dirty)
                    Dirty(uid, gun);
            }

            // Наводятся все, стреляет выбранная: сменить пушку можно без паузы на доворот.
            if (gunner.Firing && (gunner.Selected == null || gunner.Selected == uid) && Math.Abs(diff) <= AimTolerance && now >= gun.NextShot)
                FireGun((uid, gun), xform, user, gunner, now);
        }
    }

    private void FireGun(Entity<CadiaShipGunComponent> gun, TransformComponent xform, EntityUid user, CadiaShipGunnerComponent gunner, TimeSpan now)
    {
        gun.Comp.NextShot = now + gun.Comp.Cooldown;
        if (gun.Comp.Ammo <= 0)
        {
            gun.Comp.NextShot = now + TimeSpan.FromSeconds(0.6);
            _audio.PlayEntity(DryFireSound, Filter.Entities(user), gun, false);
            return;
        }
        gun.Comp.Ammo--;
        Dirty(gun);

        var dir = (gun.Comp.Angle + Angle.FromDegrees(_random.NextFloat(-gun.Comp.Spread, gun.Comp.Spread))).RotateVec(new Vector2(0, 1));
        var origin = _xform.GetMapCoordinates(gun, xform);
        var muzzle = PastOwnHull(gunner.Grid, origin, dir);

        var projectile = Spawn(gun.Comp.Projectile, muzzle);
        var velocity = TryComp<PhysicsComponent>(gunner.Grid, out var body) ? body.LinearVelocity : Vector2.Zero;
        _gun.ShootProjectile(projectile, dir, velocity, gun, user, gun.Comp.Speed);
        Spawn("CadiaFlakMuzzle", origin.Offset(dir * 0.9f));
        _audio.PlayPvs(gun.Comp.Sound, gun, AudioParams.Default.WithVariation(0.08f));

        // Из неба очередь в пустоту под прицелом ложится на землю.
        if (GroundBelow(origin.MapId) is { } ground && !_mapManager.TryFindGridAt(gunner.Aim, out _, out _))
        {
            var impact = new MapCoordinates(gunner.Aim.Position + _random.NextVector2(1.1f), ground);
            _strafes.Add((now + TimeSpan.FromSeconds(StrafeDelay), impact, gun.Comp.GroundDamage, gun.Comp.GroundExplosion, user));
        }
    }

    /// <summary>Снаряд появляется за обшивкой своего борта: иначе пушка била бы в собственную палубу.</summary>
    private MapCoordinates PastOwnHull(EntityUid grid, MapCoordinates origin, Vector2 dir)
    {
        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return origin.Offset(dir);
        for (var d = 1f; d <= 24f; d += 0.5f)
        {
            var point = origin.Offset(dir * d);
            if (!_mapManager.TryFindGridAt(point, out var found, out _) || found != grid)
                return point;
        }
        return origin.Offset(dir);
    }

    /// <summary>Поверхность под этажом неба; с других этажей по земле не стрелять и не бомбить.</summary>
    private MapId? GroundBelow(MapId map)
    {
        var uid = _map.GetMap(map);
        if (!TryComp<CadiaFlightLevelComponent>(uid, out var level) || level.Level != CadiaFlightLevel.Air)
            return null;
        return _flight.GetLevelMap(uid, -1) is { } below && TryComp<MapComponent>(below, out var comp) ? comp.MapId : null;
    }

    private void UpdateStrafes(TimeSpan now)
    {
        for (var i = _strafes.Count - 1; i >= 0; i--)
        {
            var strafe = _strafes[i];
            if (now < strafe.At)
                continue;
            _strafes.RemoveAt(i);
            Spawn("CadiaFlakBurst", strafe.Where);
            if (strafe.Explosion > 0f)
                _explosion.QueueExplosion(strafe.Where, "Default", strafe.Explosion, 5f, 10f, strafe.Source, canCreateVacuum: false);
            var damage = new DamageSpecifier(_proto.Index(PiercingDamage), strafe.Damage);
            damage += new DamageSpecifier(_proto.Index(StructuralDamage), strafe.Damage);
            _hit.Clear();
            _lookup.GetEntitiesInRange(strafe.Where, StrafeRadius, _hit);
            foreach (var target in _hit)
                _damage.TryChangeDamage(target.Owner, damage, origin: strafe.Source);
        }
    }

    private void OnGunInteract(Entity<CadiaShipGunComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<CadiaFlakAmmoComponent>(args.Used, out var ammo))
            return;
        args.Handled = true;
        ent.Comp.Ammo = Math.Min(ent.Comp.MaxAmmo, ent.Comp.Ammo + ammo.Rounds);
        Dirty(ent);
        QueueDel(args.Used);
        _audio.PlayPvs(LoadSound, ent);
        _popup.PopupEntity(Loc.GetString("cadia-ship-gun-loaded", ("ammo", ent.Comp.Ammo), ("max", ent.Comp.MaxAmmo)), ent, args.User);
    }

    private void OnGunExamined(Entity<CadiaShipGunComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cadia-ship-gun-examine", ("ammo", ent.Comp.Ammo), ("max", ent.Comp.MaxAmmo)));
    }

    // ═══════════════════════ Бомбы ═══════════════════════

    private void OnBomb(Entity<CadiaShipGunnerComponent> ent, ref CadiaShipBombActionEvent args)
    {
        if (args.Handled || _veil.Blocks(ent, predicted: false))
            return;
        args.Handled = true;
        var map = Transform(ent.Comp.Grid).MapID;
        if (GroundBelow(map) is not { } ground)
        {
            _popup.PopupEntity(Loc.GetString("cadia-ship-bomb-not-air"), ent, ent, PopupType.MediumCaution);
            _audio.PlayEntity(DenySound, Filter.Entities(ent.Owner), ent, false);
            return;
        }

        var now = _timing.CurTime;
        var velocity = TryComp<PhysicsComponent>(ent.Comp.Grid, out var body) ? body.LinearVelocity : Vector2.Zero;
        var dropped = 0;
        var left = 0;
        var bays = EntityQueryEnumerator<CadiaShipBombBayComponent, TransformComponent>();
        while (bays.MoveNext(out var uid, out var bay, out var xform))
        {
            if (xform.GridUid != ent.Comp.Grid)
                continue;
            if (bay.Bombs <= 0 || now < bay.NextDrop)
            {
                left += bay.Bombs;
                continue;
            }
            bay.Bombs--;
            bay.NextDrop = now + bay.Cooldown;
            Dirty(uid, bay);
            left += bay.Bombs;
            dropped++;

            var from = _xform.GetMapCoordinates(uid, xform);
            Spawn("CadiaFallingBomb", from);
            _audio.PlayPvs(BombReleaseSound, uid);

            // Бомба сохраняет скорость борта: точка удара впереди по курсу.
            var fall = (float) bay.FallTime.TotalSeconds;
            var point = new MapCoordinates(from.Position + velocity * fall + _random.NextVector2(0.8f), ground);
            var strike = Spawn("CadiaOrbitalStrikeMarker", point);
            var comp = EnsureComp<CadiaOrbitalStrikeComponent>(strike);
            comp.Kind = CadiaBatteryKind.Macro;
            comp.Start = now;
            comp.Impact = now + bay.FallTime;
            comp.Intensity = bay.Intensity;
            comp.Slope = 5f;
            comp.MaxTileIntensity = 18f;
            comp.Source = uid;
            Dirty(strike, comp);
        }

        _popup.PopupEntity(dropped > 0
                ? Loc.GetString("cadia-ship-bomb-dropped", ("count", dropped), ("left", left))
                : Loc.GetString("cadia-ship-bomb-empty"),
            ent, ent, dropped > 0 ? PopupType.Medium : PopupType.MediumCaution);
    }

    private void OnBayInteract(Entity<CadiaShipBombBayComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CadiaShipBombComponent>(args.Used))
            return;
        args.Handled = true;
        if (ent.Comp.Bombs >= ent.Comp.MaxBombs)
        {
            _popup.PopupEntity(Loc.GetString("cadia-ship-bay-full"), ent, args.User);
            return;
        }
        ent.Comp.Bombs++;
        Dirty(ent);
        QueueDel(args.Used);
        _audio.PlayPvs(LoadSound, ent);
        _popup.PopupEntity(Loc.GetString("cadia-ship-bay-loaded", ("bombs", ent.Comp.Bombs), ("max", ent.Comp.MaxBombs)), ent, args.User);
    }

    private void OnBayExamined(Entity<CadiaShipBombBayComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cadia-ship-bay-examine", ("bombs", ent.Comp.Bombs), ("max", ent.Comp.MaxBombs)));
    }

    // ═══════════════════════ Парашюты ═══════════════════════

    private void OnJump(Entity<CadiaParachuteComponent> ent, ref CadiaParachuteJumpActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.Performer;
        if (ent.Comp.Deployed)
        {
            _popup.PopupEntity(Loc.GetString("cadia-chute-spent"), user, user, PopupType.SmallCaution);
            return;
        }
        var here = _xform.GetMapCoordinates(user);
        if (GroundBelow(here.MapId) is not { } ground)
        {
            _popup.PopupEntity(Loc.GetString("cadia-chute-not-air"), user, user, PopupType.SmallCaution);
            return;
        }
        Deploy(ent, user, new MapCoordinates(here.Position, ground));
    }

    /// <summary>Падающий с неба боец в ранце раскрывает купол сам.</summary>
    private void OnBeforeFall(ref ZBeforeFallEvent args)
    {
        if (args.Handled || !_inventory.TryGetSlotEntity(args.Falling, "back", out var back) ||
            !TryComp<CadiaParachuteComponent>(back, out var chute) || chute.Deployed)
            return;
        if (GroundBelow(_xform.GetMapCoordinates(args.Falling).MapId) is not { } ground)
            return;
        args.Handled = true;
        Deploy((back.Value, chute), args.Falling, new MapCoordinates(args.Target.Position, ground));
    }

    private void Deploy(Entity<CadiaParachuteComponent> chute, EntityUid user, MapCoordinates target)
    {
        chute.Comp.Deployed = true;
        Dirty(chute);

        _xform.SetMapCoordinates(user, FreeLanding(target));
        _xform.AttachToGridOrMap(user);

        var falling = EnsureComp<CadiaParachutingComponent>(user);
        falling.DriftSpeed = chute.Comp.DriftSpeed;
        falling.LandAt = _timing.CurTime + chute.Comp.Descent;
        falling.Canopy = Spawn("CadiaParachuteCanopyOpening", new EntityCoordinates(user, Vector2.Zero));
        falling.OpenAt = _timing.CurTime + CanopyOpenTime;
        Dirty(user, falling);
        EnsureComp<ZFlyerComponent>(user);
        _speed.RefreshMovementSpeedModifiers(user);

        _audio.PlayPvs(ChuteOpenSound, user);
        _popup.PopupEntity(Loc.GetString("cadia-chute-open"), user, user, PopupType.Medium);
    }

    /// <summary>Точка приземления: ближайшая незанятая клетка, чтобы не оказаться внутри стены.</summary>
    private MapCoordinates FreeLanding(MapCoordinates target)
    {
        if (!_mapManager.TryFindGridAt(target, out var gridUid, out var grid))
            return target;
        var center = _map.CoordinatesToTile(gridUid, grid, _xform.ToCoordinates(target));
        for (var radius = 0; radius <= SafeLandingSearch; radius++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                        continue;
                    var tile = center + new Vector2i(x, y);
                    if (_turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid))
                        continue;
                    return _map.GridTileToWorld(gridUid, grid, tile);
                }
            }
        }
        return target;
    }

    private void UpdateChutes(TimeSpan now)
    {
        var query = EntityQueryEnumerator<CadiaParachutingComponent>();
        var landed = new List<EntityUid>();
        while (query.MoveNext(out var uid, out var falling))
        {
            if (now >= falling.LandAt)
            {
                landed.Add(uid);
                continue;
            }
            // Раскрытие играет один раз, дальше купол только покачивается.
            if (falling.OpenAt is { } openAt && now >= openAt)
            {
                falling.OpenAt = null;
                if (falling.Canopy is { } opening)
                    QueueDel(opening);
                falling.Canopy = Spawn("CadiaParachuteCanopy", new EntityCoordinates(uid, Vector2.Zero));
            }
        }
        foreach (var uid in landed)
            Land(uid);
    }

    private void Land(EntityUid user)
    {
        if (TryComp<CadiaParachutingComponent>(user, out var falling) && falling.Canopy is { } canopy)
            QueueDel(canopy);
        RemComp<CadiaParachutingComponent>(user);
        RemComp<ZFlyerComponent>(user);
        _speed.RefreshMovementSpeedModifiers(user);
        Spawn("CadiaJumpImpactEffect", _xform.GetMapCoordinates(user));
        _audio.PlayPvs(LandSound, user);
        _popup.PopupEntity(Loc.GetString("cadia-chute-landed"), user, user);
    }

    private void OnChuteExamined(Entity<CadiaParachuteComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Deployed ? "cadia-chute-examine-spent" : "cadia-chute-examine-ready"));
    }
}
