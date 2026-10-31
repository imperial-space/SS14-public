using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Imperial.ZLevels;
using Content.Server.Popups;
using Content.Server.Shuttles.Components;
using Content.Shared.Atmos;
using Content.Shared.Examine;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Зенитные позиции Кадии. Снаружи — капонир 4×4 с башней, которая поворачивается туда, куда целится наводчик.
/// Через люк заходят в бункер 3×3 (отдельная карта): огневая консоль с радаром неба и вокс-динамик стороны.
/// Наводчик кликает по радару — башня доворачивает с конечной скоростью; пока зажат спуск, позиция бьёт очередями,
/// разрывы ложатся с разбросом по этажу выше. Попадание по шаттлу — взрыв на корпусе, его гасит пустотный щит.
/// Ауспекс рядом расширяет радар и уменьшает разброс.
/// </summary>
public sealed class CadiaAntiAirSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CadiaPylonVeilSystem _veil = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly CadiaArtillerySystem _artillery = default!;
    [Dependency] private readonly CadiaFlightSystem _flight = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSub = default!;

    private static readonly SoundSpecifier HatchSound = new SoundPathSpecifier("/Audio/Machines/airlock_ext_open.ogg");
    private static readonly SoundSpecifier HatchCloseSound = new SoundPathSpecifier("/Audio/Machines/airlock_ext_close.ogg");
    private static readonly SoundSpecifier BurstSound = new SoundCollectionSpecifier("Explosion", AudioParams.Default.WithVolume(-3).WithMaxDistance(25));
    private static readonly SoundSpecifier LevelSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");
    private static readonly SoundSpecifier ViewSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/radar_ping.ogg");
    private static readonly SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    private static readonly TimeSpan UiRate = TimeSpan.FromSeconds(0.2);
    private static readonly TimeSpan DirtyRate = TimeSpan.FromSeconds(0.1);
    private static readonly TimeSpan AuspexRate = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan UnderFireRate = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BurstMemory = TimeSpan.FromSeconds(2.5);
    private const float AlignTolerance = 4f;

    /// <summary>Бункер: пол −2..2, стены по краю, внутри 3×3.</summary>
    private const int RoomHalf = 2;

    private TimeSpan _nextUi;
    private TimeSpan _nextDirty;
    private TimeSpan _nextAuspex;
    private readonly List<PendingShot> _shots = new();
    private readonly Dictionary<EntityUid, List<(CadiaAirBurst Burst, TimeSpan Time)>> _bursts = new();
    private readonly Dictionary<EntityUid, TimeSpan> _underFireWarned = new();

    private record struct PendingShot(EntityUid Gun, MapCoordinates Point, int Level, TimeSpan Impact);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CadiaAntiAirComponent, ActivateInWorldEvent>(OnGunActivate);
        SubscribeLocalEvent<CadiaAntiAirComponent, GetVerbsEvent<AlternativeVerb>>(OnGunVerbs);
        SubscribeLocalEvent<CadiaAntiAirComponent, InteractUsingEvent>(OnGunInteractUsing);
        SubscribeLocalEvent<CadiaAntiAirComponent, ExaminedEvent>(OnGunExamined);
        SubscribeLocalEvent<CadiaAntiAirComponent, EntityTerminatingEvent>(OnGunTerminating);

        SubscribeLocalEvent<CadiaEmplacementHatchComponent, ActivateInWorldEvent>(OnHatchActivate);
        SubscribeLocalEvent<CadiaAntiAirConsoleComponent, InteractUsingEvent>(OnConsoleInteractUsing);

        Subs.BuiEvents<CadiaAntiAirConsoleComponent>(CadiaAntiAirUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<BoundUIClosedEvent>(OnConsoleClosed);
            subs.Event<CadiaAntiAirAimMessage>(OnAim);
            subs.Event<CadiaAntiAirLevelMessage>(OnLevel);
            subs.Event<CadiaAntiAirFireMessage>(OnFire);
            subs.Event<CadiaAntiAirViewMessage>(OnView);
            subs.Event<CadiaAntiAirLockMessage>(OnLock);
        });

        SubscribeLocalEvent<CadiaAuspexComponent, ExaminedEvent>(OnAuspexExamined);
        SubscribeLocalEvent<CadiaAntiAirViewerComponent, ComponentShutdown>(OnViewerShutdown);
    }

    // ═══════════════════════ Камера наводчика ═══════════════════════

    /// <summary>
    /// Камера наводчика переезжает на борт и следит за ним. Сервер отправляет игроку всё вокруг борта,
    /// но речь на борту не слышна (её слушают по положению тела, а тело в бункере), существ скрывает клиент.
    /// </summary>
    private void OnView(Entity<CadiaAntiAirConsoleComponent> ent, ref CadiaAntiAirViewMessage args)
    {
        var user = args.Actor;
        if (args.Grid is not { } netGrid)
        {
            StopViewing(user);
            UpdateUi(ent);
            return;
        }

        if (ConsoleGun(ent) is not { } gun)
            return;
        var grid = GetEntity(netGrid);
        if (!FindShips(gun, RadarRange(gun), gun.Comp.Levels).Any(s => s.Grid == grid))
        {
            _popup.PopupEntity(Loc.GetString("cadia-aa-view-lost"), user, user, PopupType.MediumCaution);
            return;
        }

        if (TryComp<CadiaAntiAirViewerComponent>(user, out var old) && old.Ship is { } oldShip && TryComp<ActorComponent>(user, out var oldActor))
            _viewSub.RemoveViewSubscriber(oldShip, oldActor.PlayerSession);

        var viewer = EnsureComp<CadiaAntiAirViewerComponent>(user);
        if (viewer.Ship == null && TryComp<EyeComponent>(user, out var eye))
            viewer.HadFov = eye.DrawFov;
        viewer.Console = ent;
        viewer.Gun = gun;
        viewer.Ship = grid;
        Dirty(user, viewer);

        _eye.SetTarget(user, grid);
        _eye.SetDrawFov(user, false);
        if (TryComp<ActorComponent>(user, out var actor))
            _viewSub.AddViewSubscriber(grid, actor.PlayerSession);
        _audio.PlayEntity(ViewSound, Filter.Entities(user), ent, false);
        UpdateUi(ent);
    }

    private void StopViewing(EntityUid user)
    {
        if (HasComp<CadiaAntiAirViewerComponent>(user))
            RemComp<CadiaAntiAirViewerComponent>(user);
    }

    private void OnViewerShutdown(Entity<CadiaAntiAirViewerComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;
        if (ent.Comp.Ship is { } ship && TryComp<ActorComponent>(ent, out var actor))
            _viewSub.RemoveViewSubscriber(ship, actor.PlayerSession);
        _eye.SetTarget(ent, null);
        _eye.SetDrawFov(ent, ent.Comp.HadFov);
    }

    /// <summary>Клик прицелом по борту: точка запоминается в координатах сетки и ведётся, пока борт летит.</summary>
    private void OnLock(Entity<CadiaAntiAirConsoleComponent> ent, ref CadiaAntiAirLockMessage args)
    {
        if (ConsoleGun(ent) is not { } gun || !TryComp<CadiaAntiAirViewerComponent>(args.Actor, out var viewer) ||
            viewer.Ship is not { } ship || !TryComp<MapGridComponent>(ship, out _))
            return;

        var world = _xform.ToMapCoordinates(GetCoordinates(args.Point));
        var shipXform = Transform(ship);
        if (world.MapId != shipXform.MapID)
            return;

        var inverse = Matrix3x2.Invert(_xform.GetWorldMatrix(shipXform), out var inv) ? inv : Matrix3x2.Identity;
        gun.Comp.AimGrid = ship;
        gun.Comp.AimLocal = Vector2.Transform(world.Position, inverse);
        Dirty(gun);
        _audio.PlayEntity(LockSound, Filter.Entities(args.Actor), ent, false);
        TrackLock(gun);
        UpdateUi(ent);
    }

    /// <summary>Пересчёт прицела по захваченной точке: смещение от позиции, этаж, дальность.</summary>
    private void TrackLock(Entity<CadiaAntiAirComponent> gun)
    {
        if (gun.Comp.AimGrid is not { } grid)
            return;
        if (TerminatingOrDeleted(grid) || LevelOf(gun, grid) is not { } level)
        {
            gun.Comp.AimGrid = null;
            gun.Comp.OutOfRange = false;
            gun.Comp.Firing = false;
            Dirty(gun);
            return;
        }

        var world = Vector2.Transform(gun.Comp.AimLocal, _xform.GetWorldMatrix(grid));
        var offset = world - (_xform.GetWorldPosition(gun) + new Vector2(0.5f, 0.5f));
        var outOfRange = offset.Length() > gun.Comp.Range;
        var heading = HeadingOf(offset);
        if (gun.Comp.AimPoint != offset || gun.Comp.AimLevel != level || gun.Comp.OutOfRange != outOfRange)
        {
            gun.Comp.AimPoint = offset;
            gun.Comp.AimLevel = level;
            gun.Comp.TargetAim = heading;
            gun.Comp.OutOfRange = outOfRange;
        }
    }

    /// <summary>На сколько этажей выше позиции находится борт (null — не над этой стопкой или выше досягаемости).</summary>
    private int? LevelOf(Entity<CadiaAntiAirComponent> gun, EntityUid grid)
    {
        var map = Transform(gun).MapUid;
        var target = Transform(grid).MapUid;
        for (var i = 1; i <= gun.Comp.Levels && map != null; i++)
        {
            map = _flight.GetLevelMap(map.Value, 1);
            if (map == target)
                return i;
        }
        return null;
    }

    /// <summary>Камера отпускает борт, если наводчик ушёл с поста, борт пропал или ушёл с радара.</summary>
    private void UpdateViewers()
    {
        var query = EntityQueryEnumerator<CadiaAntiAirViewerComponent>();
        var stop = new List<EntityUid>();
        while (query.MoveNext(out var uid, out var viewer))
        {
            if (viewer.Console is not { } console || viewer.Gun is not { } gunUid || viewer.Ship is not { } ship ||
                TerminatingOrDeleted(ship) || !TryComp<CadiaAntiAirComponent>(gunUid, out var gun) ||
                !_ui.IsUiOpen(console, CadiaAntiAirUiKey.Key, uid) ||
                !FindShips(gunUid, RadarRange((gunUid, gun)), gun.Levels).Any(s => s.Grid == ship))
            {
                stop.Add(uid);
            }
        }
        foreach (var uid in stop)
        {
            _popup.PopupEntity(Loc.GetString("cadia-aa-view-lost"), uid, uid, PopupType.MediumCaution);
            StopViewing(uid);
        }
    }

    /// <summary>Силуэт борта для списка в консоли: плитки сетки, ужатые до 32×32.</summary>
    private (int W, int H, byte[] Data) Silhouette(EntityUid grid, MapGridComponent comp)
    {
        var tiles = _map.GetAllTiles(grid, comp).Select(t => t.GridIndices).ToList();
        if (tiles.Count == 0)
            return (0, 0, Array.Empty<byte>());
        var minX = tiles.Min(t => t.X);
        var minY = tiles.Min(t => t.Y);
        var w = tiles.Max(t => t.X) - minX + 1;
        var h = tiles.Max(t => t.Y) - minY + 1;
        var step = Math.Max(1, (int) Math.Ceiling(Math.Max(w, h) / 32f));
        var sw = (w + step - 1) / step;
        var sh = (h + step - 1) / step;
        var data = new byte[sw * sh];
        foreach (var t in tiles)
            data[(t.Y - minY) / step * sw + (t.X - minX) / step] = 1;
        return (sw, sh, data);
    }

    // ═══════════════════════ Бункер ═══════════════════════

    private void OnGunVerbs(Entity<CadiaAntiAirComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cadia-aa-verb-enter"),
            Priority = 2,
            Act = () => Enter(ent, user),
        });
    }

    private void OnGunActivate(Entity<CadiaAntiAirComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = true;
        Enter(ent, args.User);
    }

    private void Enter(Entity<CadiaAntiAirComponent> gun, EntityUid user)
    {
        var interior = EnsureInterior(gun);
        _audio.PlayPvs(HatchSound, gun);
        _xform.SetCoordinates(user, new EntityCoordinates(interior, new Vector2(0.5f, -0.5f)));
        _xform.AttachToGridOrMap(user);
        _audio.PlayPvs(HatchCloseSound, user);
        _popup.PopupEntity(Loc.GetString("cadia-aa-entered", ("name", Name(gun))), user, user);
    }

    private void OnHatchActivate(Entity<CadiaEmplacementHatchComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = true;

        _audio.PlayPvs(HatchSound, ent);
        if (ent.Comp.Gun is not { } gun || TerminatingOrDeleted(gun))
        {
            _popup.PopupEntity(Loc.GetString("cadia-aa-hatch-jammed"), ent, args.User);
            return;
        }
        Exit(gun, args.User);
    }

    /// <summary>Наружу — к южному краю капонира, где люк.</summary>
    private void Exit(EntityUid gun, EntityUid user)
    {
        var xform = Transform(gun);
        var exit = xform.Coordinates.Offset(new Vector2(0.5f, -2.0f));
        _xform.SetCoordinates(user, exit);
        _xform.AttachToGridOrMap(user);
        _audio.PlayPvs(HatchCloseSound, user);
    }

    /// <summary>
    /// Бункер позиции — отдельная карта: бетонный пол 5×5, по краю стены, люк в южной стене,
    /// огневая консоль у северной, вокс-динамик на стене, воздух, гравитация, тусклый свет.
    /// </summary>
    private EntityUid EnsureInterior(Entity<CadiaAntiAirComponent> gun)
    {
        if (gun.Comp.Interior is { } existing && Exists(existing))
            return existing;

        var map = _map.CreateMap(out var mapId, false);
        _meta.SetEntityName(map, Loc.GetString("cadia-aa-interior-name", ("name", Name(gun))));
        EnsureComp<CadiaEmplacementInteriorComponent>(map).Gun = gun;

        var grid = EnsureComp<MapGridComponent>(map);
        var gravity = EnsureComp<GravityComponent>(map);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(map, gravity);
        var light = EnsureComp<MapLightComponent>(map);
        light.AmbientLightColor = Color.FromHex("#4a5a48");
        Dirty(map, light);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 21.824779f;
        moles[(int) Gas.Nitrogen] = 82.10312f;
        _atmos.SetMapAtmosphere(map, false, new GasMixture(moles, Atmospherics.T20C));

        var floor = new Tile(_tiles["FloorConcrete"].TileId);
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = -RoomHalf; x <= RoomHalf; x++)
        {
            for (var y = -RoomHalf; y <= RoomHalf; y++)
                tiles.Add((new Vector2i(x, y), floor));
        }
        _map.SetTiles(map, grid, tiles);

        EntityUid At(string proto, int x, int y) => Spawn(proto, new EntityCoordinates(map, new Vector2(x + 0.5f, y + 0.5f)));

        for (var x = -RoomHalf; x <= RoomHalf; x++)
        {
            for (var y = -RoomHalf; y <= RoomHalf; y++)
            {
                if (Math.Abs(x) != RoomHalf && Math.Abs(y) != RoomHalf)
                    continue;
                if (x == 0 && y == -RoomHalf)
                    continue;
                At("CadiaWallReinforced", x, y);
            }
        }

        var hatch = At("CadiaEmplacementHatch", 0, -RoomHalf);
        EnsureComp<CadiaEmplacementHatchComponent>(hatch).Gun = gun;

        var console = At(gun.Comp.InteriorConsole, 0, 1);
        EnsureComp<CadiaAntiAirConsoleComponent>(console).Gun = gun;
        gun.Comp.InteriorConsoleEntity = console;

        At(gun.Comp.InteriorSpeaker, 1, RoomHalf);
        At("CadiaFlakAmmoBox", -1, 1);
        At("CadiaBunkerLamp", -1, RoomHalf);

        _map.InitializeMap(mapId);
        gun.Comp.Interior = map;
        return map;
    }

    /// <summary>Позицию разрушили — расчёт выбрасывает наружу, бункер исчезает.</summary>
    private void OnGunTerminating(Entity<CadiaAntiAirComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Interior is not { } interior || !Exists(interior))
            return;

        var coords = Transform(ent).Coordinates;
        var occupants = new List<EntityUid>();
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid == interior)
                occupants.Add(uid);
        }
        foreach (var uid in occupants)
        {
            _xform.SetCoordinates(uid, coords.Offset(_random.NextVector2(1.5f)));
            _xform.AttachToGridOrMap(uid);
            _popup.PopupEntity(Loc.GetString("cadia-aa-destroyed-inside"), uid, uid, PopupType.LargeCaution);
        }
        QueueDel(interior);
    }

    // ═══════════════════════ Боеприпасы ═══════════════════════

    private void OnGunInteractUsing(Entity<CadiaAntiAirComponent> ent, ref InteractUsingEvent args)
    {
        if (!args.Handled && TryLoad(ent, args.Used, args.User))
            args.Handled = true;
    }

    private void OnConsoleInteractUsing(Entity<CadiaAntiAirConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Gun is not { } gun || !TryComp<CadiaAntiAirComponent>(gun, out var aa))
            return;
        if (TryLoad((gun, aa), args.Used, args.User))
            args.Handled = true;
    }

    private bool TryLoad(Entity<CadiaAntiAirComponent> gun, EntityUid used, EntityUid user)
    {
        if (!TryComp<CadiaFlakAmmoComponent>(used, out var ammo))
            return false;
        if (gun.Comp.Ammo >= gun.Comp.MaxAmmo)
        {
            _popup.PopupEntity(Loc.GetString("cadia-aa-full"), user, user);
            return true;
        }
        gun.Comp.Ammo = Math.Min(gun.Comp.MaxAmmo, gun.Comp.Ammo + ammo.Rounds);
        Dirty(gun);
        QueueDel(used);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/lmg_magin.ogg"), user);
        _popup.PopupEntity(Loc.GetString("cadia-aa-loaded", ("ammo", gun.Comp.Ammo), ("max", gun.Comp.MaxAmmo)), user, user);
        return true;
    }

    private void OnGunExamined(Entity<CadiaAntiAirComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cadia-aa-examine-ammo", ("ammo", ent.Comp.Ammo), ("max", ent.Comp.MaxAmmo)));
        args.PushMarkup(Loc.GetString("cadia-aa-examine-enter"));
    }

    // ═══════════════════════ Консоль ═══════════════════════

    private Entity<CadiaAntiAirComponent>? ConsoleGun(EntityUid console)
    {
        if (!TryComp<CadiaAntiAirConsoleComponent>(console, out var comp) || comp.Gun is not { } gun ||
            !TryComp<CadiaAntiAirComponent>(gun, out var aa))
            return null;
        return (gun, aa);
    }

    private void OnConsoleOpened(Entity<CadiaAntiAirConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnConsoleClosed(Entity<CadiaAntiAirConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        // Закрыл пост — камера возвращается в бункер.
        StopViewing(args.Actor);

        // Наводчик ушёл с поста — спуск отпущен.
        if (_ui.IsUiOpen(ent.Owner, CadiaAntiAirUiKey.Key) || ConsoleGun(ent) is not { } gun)
            return;
        gun.Comp.Firing = false;
        Dirty(gun);
    }

    private void OnAim(Entity<CadiaAntiAirConsoleComponent> ent, ref CadiaAntiAirAimMessage args)
    {
        if (ConsoleGun(ent) is not { } gun)
            return;
        var offset = args.Offset;
        var range = RadarRange(gun);
        if (offset.Length() > range)
            offset = offset.Normalized() * range;
        if (offset.LengthSquared() < 0.01f)
            return;
        // Наводка по радару — «в точку неба», захват борта сбрасывается.
        gun.Comp.AimGrid = null;
        gun.Comp.OutOfRange = false;
        gun.Comp.AimPoint = offset;
        gun.Comp.TargetAim = HeadingOf(offset);
        Dirty(gun);
        UpdateUi(ent);
    }

    private void OnLevel(Entity<CadiaAntiAirConsoleComponent> ent, ref CadiaAntiAirLevelMessage args)
    {
        if (ConsoleGun(ent) is not { } gun)
            return;
        gun.Comp.AimLevel = Math.Clamp(args.Level, 1, gun.Comp.Levels);
        Dirty(gun);
        _audio.PlayPvs(LevelSound, ent);
        UpdateUi(ent);
    }

    private void OnFire(Entity<CadiaAntiAirConsoleComponent> ent, ref CadiaAntiAirFireMessage args)
    {
        if (ConsoleGun(ent) is not { } gun)
            return;
        gun.Comp.Firing = args.Firing && !_veil.Blocks(args.Actor, predicted: false);
        Dirty(gun);
    }

    /// <summary>Угол поворота спрайта башни (стволы на север) к смещению.</summary>
    private static Angle HeadingOf(Vector2 offset)
    {
        return new Angle(MathF.Atan2(-offset.X, offset.Y));
    }

    private float RadarRange(Entity<CadiaAntiAirComponent> gun)
    {
        return gun.Comp.Range * (LinkedAuspex(gun) != null ? 1.5f : 1f);
    }

    private CadiaAuspexComponent? LinkedAuspex(Entity<CadiaAntiAirComponent> gun)
    {
        var xform = Transform(gun);
        var position = _xform.GetWorldPosition(xform);
        var query = EntityQueryEnumerator<CadiaAuspexComponent, TransformComponent>();
        while (query.MoveNext(out _, out var auspex, out var axform))
        {
            if (auspex.Faction == gun.Comp.Faction && axform.MapUid == xform.MapUid && axform.Anchored &&
                (_xform.GetWorldPosition(axform) - position).Length() <= auspex.LinkRange)
                return auspex;
        }
        return null;
    }

    private void UpdateUi(Entity<CadiaAntiAirConsoleComponent> console)
    {
        var state = new CadiaAntiAirBuiState();
        if (ConsoleGun(console) is { } gun)
        {
            var range = RadarRange(gun);
            state.Linked = true;
            state.GunName = Name(gun);
            state.Range = range;
            state.Levels = gun.Comp.Levels;
            state.AimLevel = gun.Comp.AimLevel;
            state.Aim = gun.Comp.Aim;
            state.TargetAim = gun.Comp.TargetAim;
            state.AimPoint = gun.Comp.AimPoint;
            state.Firing = gun.Comp.Firing;
            state.Ammo = gun.Comp.Ammo;
            state.MaxAmmo = gun.Comp.MaxAmmo;
            state.AuspexLinked = LinkedAuspex(gun) != null;
            state.AimGrid = gun.Comp.AimGrid is { } aimGrid ? GetNetEntity(aimGrid) : null;
            state.OutOfRange = gun.Comp.OutOfRange;

            foreach (var (grid, offset, level) in FindShips(gun, range, gun.Comp.Levels))
            {
                var kind = _artillery.ShipFaction(grid) switch
                {
                    null => CadiaAirContactKind.Neutral,
                    var f when f == gun.Comp.Faction => CadiaAirContactKind.Friendly,
                    _ => CadiaAirContactKind.Enemy,
                };
                var size = TryComp<MapGridComponent>(grid, out var g) ? g.LocalAABB.Size.Length() / 2f : 2f;
                var (sw, sh, sil) = g != null ? Silhouette(grid, g) : (0, 0, Array.Empty<byte>());
                state.Contacts.Add(new CadiaAirContact(GetNetEntity(grid), offset, level, size, kind, Name(grid), sw, sh, sil));
            }

            if (_bursts.TryGetValue(gun, out var bursts))
                state.Bursts.AddRange(bursts.Select(b => b.Burst));
        }
        _ui.SetUiState(console.Owner, CadiaAntiAirUiKey.Key, state);
    }

    /// <summary>Шаттлы на этажах выше в радиусе по горизонтали: смещение центра от позиции и номер этажа.</summary>
    private List<(EntityUid Grid, Vector2 Offset, int Level)> FindShips(EntityUid source, float range, int levels)
    {
        var result = new List<(EntityUid, Vector2, int)>();
        var xform = Transform(source);
        if (xform.MapUid is not { } map)
            return result;
        var position = _xform.GetWorldPosition(xform);

        var level = map;
        for (var i = 1; i <= levels; i++)
        {
            if (_flight.GetLevelMap(level, 1) is not { } above)
                break;
            level = above;

            var shuttles = EntityQueryEnumerator<ShuttleComponent, MapGridComponent, TransformComponent>();
            while (shuttles.MoveNext(out var grid, out _, out var gridComp, out var gridXform))
            {
                if (gridXform.MapUid != level || grid == level || HasComp<FTLComponent>(grid))
                    continue;
                var center = Vector2.Transform(gridComp.LocalAABB.Center, _xform.GetWorldMatrix(gridXform));
                var offset = center - position;
                if (offset.Length() <= range)
                    result.Add((grid, offset, i));
            }
        }
        return result;
    }

    // ═══════════════════════ Башня и огонь ═══════════════════════

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var dirty = now >= _nextDirty;
        if (dirty)
            _nextDirty = now + DirtyRate;

        var query = EntityQueryEnumerator<CadiaAntiAirComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var aa, out var xform))
        {
            // Поворот башни с конечной скоростью по кратчайшей дуге.
            var diff = Angle.ShortestDistance(aa.Aim, aa.TargetAim).Degrees;
            if (Math.Abs(diff) > 0.01)
            {
                var step = aa.TurnRate * frameTime;
                aa.Aim = Math.Abs(diff) <= step ? aa.TargetAim : aa.Aim + Angle.FromDegrees(Math.Sign(diff) * step);
                if (dirty)
                    Dirty(uid, aa);
                if (now >= aa.NextTurnSound)
                {
                    aa.NextTurnSound = now + TimeSpan.FromSeconds(1.2);
                    _audio.PlayPvs(aa.TurnSound, uid, AudioParams.Default.WithVolume(-6).WithPitchScale(0.7f));
                }
            }

            TrackLock((uid, aa));
            if (!aa.Firing || aa.OutOfRange || aa.AimPoint is not { } aimPoint || now < aa.NextShot || !xform.Anchored)
                continue;
            if (Math.Abs(Angle.ShortestDistance(aa.Aim, aa.TargetAim).Degrees) > AlignTolerance)
                continue;
            if (aa.Ammo <= 0)
            {
                aa.Firing = false;
                Dirty(uid, aa);
                if (aa.InteriorConsoleEntity is { } console)
                    _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/Guns/EmptyAlarm/lmg_empty_alarm.ogg"), console);
                continue;
            }

            Fire((uid, aa), xform, aimPoint, now);
        }

        ResolveShots(now);

        if (dirty)
            UpdateViewers();

        if (now >= _nextAuspex)
        {
            _nextAuspex = now + AuspexRate;
            UpdateAuspex();
        }

        if (now >= _nextUi)
        {
            _nextUi = now + UiRate;
            var consoles = EntityQueryEnumerator<CadiaAntiAirConsoleComponent>();
            while (consoles.MoveNext(out var uid, out var console))
            {
                if (_ui.IsUiOpen(uid, CadiaAntiAirUiKey.Key))
                    UpdateUi((uid, console));
            }
            foreach (var list in _bursts.Values)
                list.RemoveAll(b => now - b.Time > BurstMemory);
        }
    }

    private void Fire(Entity<CadiaAntiAirComponent> gun, TransformComponent xform, Vector2 aimPoint, TimeSpan now)
    {
        gun.Comp.NextShot = now + gun.Comp.FireRate;
        gun.Comp.Ammo--;
        Dirty(gun);

        // Вспышка у среза стволов, грохот очереди.
        var dir = gun.Comp.Aim.RotateVec(new Vector2(0, 1));
        var center = xform.Coordinates.Offset(new Vector2(0.5f, 0.5f));
        Spawn(gun.Comp.MuzzleEffect, center.Offset(dir * 1.7f));
        _audio.PlayPvs(gun.Comp.FireSound, gun, AudioParams.Default.WithVolume(3).WithVariation(0.06f));

        // Куда ляжет разрыв: этаж выше, разброс растёт с дальностью, ауспекс его сжимает.
        var level = xform.MapUid;
        for (var i = 0; i < gun.Comp.AimLevel && level != null; i++)
            level = _flight.GetLevelMap(level.Value, 1);
        if (level == null)
            return;

        var distance = aimPoint.Length();
        var spread = gun.Comp.Spread * Math.Max(0.25f, distance / gun.Comp.Range);
        if (LinkedAuspex(gun) is { } auspex)
            spread *= auspex.SpreadFactor;
        var point = _xform.GetWorldPosition(xform) + new Vector2(0.5f, 0.5f) + aimPoint + _random.NextVector2(spread);
        var flight = TimeSpan.FromSeconds(Math.Max(0.15f, (distance + gun.Comp.AimLevel * 10f) / gun.Comp.ShellSpeed));
        _shots.Add(new PendingShot(gun, new MapCoordinates(point, Comp<MapComponent>(level.Value).MapId), gun.Comp.AimLevel, now + flight));
    }

    private void ResolveShots(TimeSpan now)
    {
        for (var i = _shots.Count - 1; i >= 0; i--)
        {
            var shot = _shots[i];
            if (now < shot.Impact)
                continue;
            _shots.RemoveAt(i);

            if (!TryComp<CadiaAntiAirComponent>(shot.Gun, out var gun))
                continue;

            Spawn(gun.BurstEffect, shot.Point);
            _audio.PlayPvs(BurstSound, Spawn(null, shot.Point));

            var hit = _mapManager.TryFindGridAt(shot.Point, out var grid, out _) &&
                      HasComp<ShuttleComponent>(grid) && !HasComp<MapComponent>(grid);

            var gunPos = _xform.GetWorldPosition(shot.Gun) + new Vector2(0.5f, 0.5f);
            if (!_bursts.TryGetValue(shot.Gun, out var list))
                _bursts[shot.Gun] = list = new();
            list.Add((new CadiaAirBurst(shot.Point.Position - gunPos, shot.Level, hit), now));

            if (!hit)
                continue;

            WarnUnderFire(grid);
            if (_artillery.TryAbsorb(shot.Point, gun.Damage))
                continue;

            var vacuum = TryComp<CadiaFlightLevelComponent>(Transform(grid).MapUid, out var levelComp) && levelComp.Level == CadiaFlightLevel.Orbit;
            _explosion.QueueExplosion(shot.Point, "Default", gun.Damage, 3f, 6f, shot.Gun, canCreateVacuum: vacuum);
            _artillery.ShakeGrid(grid, 1.2f);
        }
    }

    private void WarnUnderFire(EntityUid grid)
    {
        var now = _timing.CurTime;
        if (_underFireWarned.TryGetValue(grid, out var next) && now < next)
            return;
        _underFireWarned[grid] = now + UnderFireRate;
        _artillery.PopupGrid(grid, Loc.GetString("cadia-aa-under-fire"), PopupType.LargeCaution);
    }

    // ═══════════════════════ Ауспекс ═══════════════════════

    private void UpdateAuspex()
    {
        var query = EntityQueryEnumerator<CadiaAuspexComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var auspex, out var xform))
        {
            if (!xform.Anchored)
                continue;

            var current = FindShips(uid, auspex.Range, 2)
                .Where(s => _artillery.ShipFaction(s.Grid) is { } f && f != auspex.Faction)
                .Select(s => s.Grid)
                .ToHashSet();
            var fresh = current.Where(t => !auspex.Contacts.Contains(t)).ToList();
            auspex.Contacts = current;
            if (auspex.ContactCount != current.Count)
            {
                auspex.ContactCount = current.Count;
                Dirty(uid, auspex);
            }
            if (fresh.Count == 0)
                continue;

            _audio.PlayPvs(auspex.ContactSound, uid, AudioParams.Default.WithVolume(4));
            var text = Loc.GetString("cadia-auspex-contact", ("count", fresh.Count), ("bearing", Bearing(uid, fresh[0])));
            var faction = auspex.Faction;
            var map = xform.MapUid;
            var filter = Filter.Empty().AddWhereAttachedEntity(e =>
            {
                if (!TryComp<CadiaFactionMemberComponent>(e, out var m) || m.Role == null || m.Faction != faction)
                    return false;
                // Слышат и на поверхности, и в бункерах зенитных позиций.
                var mobMap = Transform(e).MapUid;
                return mobMap == map || HasComp<CadiaEmplacementInteriorComponent>(mobMap);
            });
            foreach (var session in filter.Recipients)
            {
                if (session.AttachedEntity is { } mob)
                    _popup.PopupEntity(text, mob, mob, PopupType.MediumCaution);
            }
        }
    }

    private string Bearing(EntityUid from, EntityUid grid)
    {
        var delta = _xform.GetWorldPosition(grid) - _xform.GetWorldPosition(from);
        var dir = delta.LengthSquared() < 1f ? Direction.Invalid : new Angle(delta).GetDir();
        var name = dir == Direction.Invalid ? Loc.GetString("cadia-bearing-overhead") : Loc.GetString($"cadia-bearing-{dir.ToString().ToLowerInvariant()}");
        return Loc.GetString("cadia-auspex-bearing", ("dir", name), ("distance", (int) delta.Length()));
    }

    private void OnAuspexExamined(Entity<CadiaAuspexComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Contacts.Count == 0)
        {
            args.PushMarkup(Loc.GetString("cadia-auspex-examine-clear"));
            return;
        }
        args.PushMarkup(Loc.GetString("cadia-auspex-examine-contacts", ("count", ent.Comp.Contacts.Count)));
        foreach (var contact in ent.Comp.Contacts)
        {
            if (!Exists(contact))
                continue;
            var level = TryComp<CadiaFlightLevelComponent>(Transform(contact).MapUid, out var l) ? l.Level : CadiaFlightLevel.Air;
            args.PushMarkup(Loc.GetString("cadia-auspex-examine-line", ("name", Name(contact)), ("bearing", Bearing(ent, contact)),
                ("level", Loc.GetString($"cadia-flight-beacon-{level.ToString().ToLowerInvariant()}"))));
        }
    }
}
