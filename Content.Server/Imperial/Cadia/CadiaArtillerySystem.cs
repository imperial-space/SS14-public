using System.Linq;
using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Imperial.ZLevels;
using Content.Server.Popups;
using Content.Server.Shuttles.Components;
using Content.Shared.Camera;
using Content.Shared.Shuttles.Components;
using Content.Shared.Examine;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Орбитальный обстрел и ПВО Кадии.
/// Батареи кораблей на орбите наводятся по голокарте поверхности под кораблём; удар предупреждается красным кругом
/// на земле. Пустотные щиты кораблей поглощают удары и попадания, пока не погаснут. Зенитки и ракетные установки
/// бьют по шаттлам врага на этажах выше, ауспекс находит цели и наводит зенитки точнее.
/// </summary>
public sealed class CadiaArtillerySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CadiaPylonVeilSystem _veil = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CadiaMoraleSystem _morale = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly CadiaFlightSystem _flight = default!;
    [Dependency] private readonly CadiaHoloTableSystem _holo = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;

    private static readonly SoundSpecifier IncomingSound = new SoundPathSpecifier("/Audio/Imperial/Cargo/mortar_long_whistle.ogg", AudioParams.Default.WithVolume(6).WithMaxDistance(30));
    private static readonly SoundSpecifier MarkSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/radar_ping.ogg", AudioParams.Default.WithVolume(2));
    private static readonly SoundSpecifier LanceSound = new SoundPathSpecifier("/Audio/Weapons/emitter.ogg", AudioParams.Default.WithVolume(10).WithMaxDistance(40));
    private static readonly SoundSpecifier FarBoom = new SoundPathSpecifier("/Audio/Effects/explosionfar.ogg");
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
    private static readonly SoundSpecifier TargetSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");

    private const int MaxWindow = 129;
    private static readonly TimeSpan UiRate = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WhistleLead = TimeSpan.FromSeconds(1.2);

    private TimeSpan _nextUi;
    private readonly HashSet<EntityUid> _whistled = new();
    private readonly HashSet<EntityUid> _static = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<CadiaOrbitalBatteryComponent>(CadiaBatteryUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<CadiaBatterySetTargetMessage>(OnSetTarget);
            subs.Event<CadiaBatteryFireMessage>(OnFire);
        });
        SubscribeLocalEvent<CadiaOrbitalBatteryComponent, InteractUsingEvent>(OnLoadShell);
        SubscribeLocalEvent<CadiaOrbitalBatteryComponent, ExaminedEvent>(OnBatteryExamined);
        SubscribeLocalEvent<CadiaVoidShieldComponent, ExaminedEvent>(OnShieldExamined);
        SubscribeLocalEvent<CadiaVoidShieldComponent, MapInitEvent>(OnShieldInit);
    }

    // ═══════════════════════ Батареи ═══════════════════════

    /// <summary>
    /// Где стоит корабль, наводимый с земли: пост флота в бункере под Касром ведёт огонь крейсера, который держится
    /// над полем в этой точке. От неё считаются дальность и окно голокарты.
    /// </summary>
    private static readonly Vector2 GroundStation = new(-20f, 0f);

    /// <summary>Пост батареи на поверхности или под ней (бункер на этаже поля), а не на корабле.</summary>
    private bool OnGround(EntityUid battery, out EntityUid surface)
    {
        surface = default;
        if (Transform(battery).MapUid is not { } map)
            return false;
        // Сама поверхность или этаж над и под ней: идём вверх по стопке, пока не найдём поверхность поля.
        var current = (EntityUid?) map;
        for (var i = 0; i < 3 && current is { } level; i++)
        {
            if (TryComp<CadiaFlightLevelComponent>(level, out var flight))
            {
                if (flight.Level != CadiaFlightLevel.Surface)
                    return false;
                surface = level;
                return true;
            }
            current = _flight.GetLevelMap(level, 1);
        }
        return false;
    }

    /// <summary>Поверхность под кораблём батареи; null с причиной, если корабль не на орбите Кадии.</summary>
    private EntityUid? GetSurfaceBelow(EntityUid battery, out string? denial)
    {
        denial = null;
        if (OnGround(battery, out var ground))
            return ground;
        var map = Transform(battery).MapUid;
        if (map == null || !TryComp<CadiaFlightLevelComponent>(map, out var level) || level.Level != CadiaFlightLevel.Orbit)
        {
            denial = Loc.GetString("cadia-battery-not-orbit");
            return null;
        }

        for (var below = _flight.GetLevelMap(map.Value, -1); below != null; below = _flight.GetLevelMap(below.Value, -1))
        {
            if (TryComp<CadiaFlightLevelComponent>(below, out var l) && l.Level == CadiaFlightLevel.Surface)
                return below;
        }
        denial = Loc.GetString("cadia-battery-no-surface");
        return null;
    }

    private void OnUiOpened(Entity<CadiaOrbitalBatteryComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<CadiaOrbitalBatteryComponent> ent)
    {
        var state = new CadiaBatteryBuiState
        {
            Kind = ent.Comp.Kind,
            Ammo = ent.Comp.Ammo,
            MaxAmmo = ent.Comp.MaxAmmo,
            NextShot = ent.Comp.NextShot,
            Range = ent.Comp.Range,
            Spread = ent.Comp.Kind == CadiaBatteryKind.Lance ? 0.6f : ent.Comp.Spread,
            Target = ent.Comp.Target,
        };

        var surface = GetSurfaceBelow(ent, out var denial);
        state.Denial = denial;
        state.InOrbit = surface != null;
        var shipPos = ShipPosition(ent);
        state.ShipPosition = shipPos;

        if (surface is { } surfaceUid)
            BuildHolomap(ent, surfaceUid, shipPos, state);

        _ui.SetUiState(ent.Owner, CadiaBatteryUiKey.Key, state);
    }

    /// <summary>Окно поверхности вокруг точки под кораблём: земля, постройки и отметки (свои, пилоны, цитадель, удары).</summary>
    private void BuildHolomap(Entity<CadiaOrbitalBatteryComponent> ent, EntityUid surface, Vector2 center, CadiaBatteryBuiState state)
    {
        var size = Math.Min(MaxWindow, (int) (ent.Comp.Range * 2) + 1);
        var origin = new Vector2i((int) Math.Floor(center.X) - size / 2, (int) Math.Floor(center.Y) - size / 2);
        var tiles = new byte[size * size];
        state.Origin = origin;
        state.Size = size;
        state.Tiles = tiles;

        var mapId = Comp<MapComponent>(surface).MapId;
        if (TryComp<MapGridComponent>(surface, out var grid))
        {
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (_map.TryGetTileRef(surface, grid, new Vector2i(origin.X + x, origin.Y + y), out var tile) && !tile.Tile.IsEmpty)
                        tiles[y * size + x] = 1;
                }
            }
        }

        // Посадочные сетки (шаттлы, бункеры) на поверхности — их палубы тоже земля.
        var box = new Box2(origin, origin + new Vector2i(size, size));
        var grids = new List<Entity<MapGridComponent>>();
        _mapManager.FindGridsIntersecting(mapId, box, ref grids);
        foreach (var other in grids)
        {
            if (other.Owner == surface)
                continue;
            state.Markers.Add(new CadiaHoloMarker(CadiaHoloMarkerKind.Shuttle, _xform.GetWorldPosition(other)));
        }

        _static.Clear();
        _lookup.GetEntitiesIntersecting(mapId, box, _static, LookupFlags.Static);
        foreach (var uid in _static)
        {
            if (!IsSolid(uid))
                continue;
            var pos = _xform.GetWorldPosition(uid) - origin;
            var tx = (int) Math.Floor(pos.X);
            var ty = (int) Math.Floor(pos.Y);
            if (tx >= 0 && ty >= 0 && tx < size && ty < size)
                tiles[ty * size + tx] = 2;
        }

        // Пост на земле — бункер гвардии; на корабле — сторона экипажа.
        var faction = OnGround(ent, out _) ? CadiaFaction.Imperium : ShipFaction(Transform(ent).GridUid ?? ent.Owner) ?? CadiaFaction.Imperium;
        state.Markers.Add(new CadiaHoloMarker(CadiaHoloMarkerKind.Ship, center));
        foreach (var target in _holo.GetTargets(faction))
            state.Markers.Add(new CadiaHoloMarker(CadiaHoloMarkerKind.Designated, target));

        var members = EntityQueryEnumerator<CadiaFactionMemberComponent, TransformComponent>();
        while (members.MoveNext(out var uid, out var member, out var xform))
        {
            if (xform.MapUid != surface || member.Role == null || member.Faction != faction || _mobState.IsDead(uid))
                continue;
            state.Markers.Add(new CadiaHoloMarker(CadiaHoloMarkerKind.Friendly, _xform.GetWorldPosition(xform)));
        }

        var pylons = EntityQueryEnumerator<CadiaPylonComponent, TransformComponent>();
        while (pylons.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid == surface)
                state.Markers.Add(new CadiaHoloMarker(CadiaHoloMarkerKind.Pylon, _xform.GetWorldPosition(xform)));
        }

        var citadels = EntityQueryEnumerator<CadiaCitadelComponent, TransformComponent>();
        while (citadels.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid == surface)
                state.Markers.Add(new CadiaHoloMarker(CadiaHoloMarkerKind.Citadel, _xform.GetWorldPosition(xform)));
        }

        var strikes = EntityQueryEnumerator<CadiaOrbitalStrikeComponent, TransformComponent>();
        while (strikes.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid == surface)
                state.Markers.Add(new CadiaHoloMarker(CadiaHoloMarkerKind.Strike, _xform.GetWorldPosition(xform)));
        }
    }

    private bool IsSolid(EntityUid uid)
    {
        if (!TryComp<PhysicsComponent>(uid, out var body) || !body.CanCollide || !TryComp<FixturesComponent>(uid, out var fixtures))
            return false;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard && (fixture.CollisionLayer & (int) (CollisionGroup.Impassable | CollisionGroup.HighImpassable)) != 0)
                return true;
        }
        return false;
    }

    private void OnSetTarget(Entity<CadiaOrbitalBatteryComponent> ent, ref CadiaBatterySetTargetMessage args)
    {
        if (GetSurfaceBelow(ent, out _) == null)
            return;

        var center = ShipPosition(ent);
        var offset = args.Target - center;
        if (offset.Length() > ent.Comp.Range)
            offset = offset.Normalized() * ent.Comp.Range;
        ent.Comp.Target = center + offset;
        Dirty(ent);
        _audio.PlayPvs(TargetSound, ent);
        UpdateUi(ent);
    }

    private void OnFire(Entity<CadiaOrbitalBatteryComponent> ent, ref CadiaBatteryFireMessage args)
    {
        var user = args.Actor;
        var surface = GetSurfaceBelow(ent, out var denial);
        string? reason = denial;
        var now = _timing.CurTime;
        if (reason == null && _veil.Active())
            reason = Loc.GetString("cadia-veil-orbit");

        if (reason == null && ent.Comp.Target == null)
            reason = Loc.GetString("cadia-battery-no-target");
        if (reason == null && now < ent.Comp.NextShot)
            reason = Loc.GetString("cadia-battery-reloading", ("seconds", (int) Math.Ceiling((ent.Comp.NextShot - now).TotalSeconds)));
        if (reason == null && ent.Comp.Kind == CadiaBatteryKind.Macro && ent.Comp.Ammo <= 0)
            reason = Loc.GetString("cadia-battery-no-ammo");

        if (reason != null || surface == null || ent.Comp.Target is not { } target)
        {
            _popup.PopupEntity(reason ?? string.Empty, ent, user, PopupType.MediumCaution);
            _audio.PlayEntity(DenySound, Filter.Entities(user), ent, false);
            return;
        }

        var ground = OnGround(ent, out _);
        var astropathDelay = (ground || Transform(ent).GridUid is { } firingShip && ShipFaction(firingShip) == CadiaFaction.Imperium) && !AstropathAlive();
        var lance = ent.Comp.Kind == CadiaBatteryKind.Lance;
        var count = lance ? 1 : Math.Min(ent.Comp.Salvo, ent.Comp.Ammo);
        if (!lance)
            ent.Comp.Ammo -= count;
        ent.Comp.NextShot = now + ent.Comp.Reload;
        Dirty(ent);

        for (var i = 0; i < count; i++)
        {
            var spread = lance ? 0.6f : ent.Comp.Spread;
            var point = target + _random.NextVector2(spread);
            var delay = ent.Comp.Warning + TimeSpan.FromSeconds(lance ? 0 : _random.NextFloat(0f, 1.6f));
            // Без астропата связь с флотом идёт обычным воксом: удар заметно запаздывает.
            if (astropathDelay)
                delay *= 1.5;
            var strike = Spawn(lance ? "CadiaLanceStrikeMarker" : "CadiaOrbitalStrikeMarker", new EntityCoordinates(surface.Value, point));
            var comp = EnsureComp<CadiaOrbitalStrikeComponent>(strike);
            comp.Kind = ent.Comp.Kind;
            comp.Start = now;
            comp.Impact = now + delay;
            comp.Intensity = ent.Comp.Intensity;
            comp.Slope = ent.Comp.Slope;
            comp.MaxTileIntensity = ent.Comp.MaxTileIntensity;
            comp.Source = ent;
            Dirty(strike, comp);
            _audio.PlayPvs(MarkSound, strike);
        }

        // Залп сотрясает корабль: орудия гремят, экипаж трясёт. Пост на земле только передаёт приказ крейсеру.
        var fire = _audio.PlayPvs(ent.Comp.FireSound, ent, AudioParams.Default.WithVolume(ground ? -4 : 4));
        if (!ground)
        {
            _audio.SetGridAudio(fire);
            if (Transform(ent).GridUid is { } shipGrid)
                ShakeGrid(shipGrid, lance ? 1.2f : 2f);
        }

        _popup.PopupEntity(Loc.GetString(lance ? "cadia-battery-fired-lance" : "cadia-battery-fired-macro", ("count", count)), ent, user, PopupType.Medium);
        UpdateUi(ent);
    }

    private bool AstropathAlive()
    {
        var members = EntityQueryEnumerator<CadiaFactionMemberComponent>();
        while (members.MoveNext(out var uid, out var member))
        {
            if (member.Role == "CadiaAstropath" && !_mobState.IsDead(uid))
                return true;
        }
        return false;
    }

    private void OnLoadShell(Entity<CadiaOrbitalBatteryComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<CadiaMacroShellComponent>(args.Used, out var shell))
            return;
        args.Handled = true;

        if (ent.Comp.Kind != CadiaBatteryKind.Macro)
        {
            _popup.PopupEntity(Loc.GetString("cadia-battery-lance-no-ammo"), ent, args.User);
            return;
        }
        if (ent.Comp.Ammo >= ent.Comp.MaxAmmo)
        {
            _popup.PopupEntity(Loc.GetString("cadia-battery-full"), ent, args.User);
            return;
        }

        ent.Comp.Ammo = Math.Min(ent.Comp.MaxAmmo, ent.Comp.Ammo + shell.Rounds);
        Dirty(ent);
        QueueDel(args.Used);
        _audio.PlayPvs(ent.Comp.LoadSound, ent);
        _popup.PopupEntity(Loc.GetString("cadia-battery-loaded", ("ammo", ent.Comp.Ammo), ("max", ent.Comp.MaxAmmo)), ent, args.User);
        UpdateUi(ent);
    }

    private void OnBatteryExamined(Entity<CadiaOrbitalBatteryComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Kind == CadiaBatteryKind.Macro)
            args.PushMarkup(Loc.GetString("cadia-battery-examine-ammo", ("ammo", ent.Comp.Ammo), ("max", ent.Comp.MaxAmmo)));
        var left = ent.Comp.NextShot - _timing.CurTime;
        args.PushMarkup(left > TimeSpan.Zero
            ? Loc.GetString("cadia-battery-examine-reload", ("seconds", (int) Math.Ceiling(left.TotalSeconds)))
            : Loc.GetString("cadia-battery-examine-ready"));
    }

    private void UpdateStrikes(TimeSpan now)
    {
        var query = EntityQueryEnumerator<CadiaOrbitalStrikeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var strike, out var xform))
        {
            if (strike.Kind == CadiaBatteryKind.Macro && now >= strike.Impact - WhistleLead && _whistled.Add(uid))
                _audio.PlayPvs(IncomingSound, uid);

            if (now < strike.Impact)
                continue;

            _whistled.Remove(uid);
            var coords = _xform.GetMapCoordinates(uid, xform);
            QueueDel(uid);

            if (TryAbsorb(coords, strike.Intensity / 4f))
                continue;

            _morale.Shock(coords, 10f, -15f);

            if (strike.Kind == CadiaBatteryKind.Lance)
            {
                Spawn("CadiaLanceBeam", coords);
                _audio.PlayPvs(LanceSound, Spawn(null, coords));
                _explosion.QueueExplosion(coords, "Default", strike.Intensity, strike.Slope * 2f, strike.MaxTileIntensity * 1.6f, strike.Source, canCreateVacuum: false);
            }
            else
            {
                _explosion.QueueExplosion(coords, "Default", strike.Intensity, strike.Slope, strike.MaxTileIntensity, strike.Source, canCreateVacuum: false);
            }

            // Далёкий грохот слышит вся поверхность.
            _audio.PlayGlobal(FarBoom, Filter.BroadcastMap(coords.MapId), false, AudioParams.Default.WithVolume(-6));
        }
    }

    // ═══════════════════════ Пустотный щит ═══════════════════════

    private void OnShieldInit(Entity<CadiaVoidShieldComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, CadiaVoidShieldVisuals.Up, ent.Comp.RebootAt == null);
    }

    /// <summary>Откуда бьёт батарея: корабль на орбите — его место, пост на земле — крейсер над полем.</summary>
    private Vector2 ShipPosition(EntityUid battery)
    {
        if (OnGround(battery, out _))
            return GroundStation;
        return _xform.GetWorldPosition(Transform(battery).GridUid is { } grid ? grid : battery);
    }

    /// <summary>
    /// Поглощает удар по сетке с поднятым пустотным щитом. Щит на земле прикрывает только свой круг.
    /// Истощённый щит гаснет до перезапуска.
    /// </summary>
    public bool TryAbsorb(MapCoordinates coords, float damage)
    {
        if (!_mapManager.TryFindGridAt(coords, out var grid, out _))
            return false;

        var query = EntityQueryEnumerator<CadiaVoidShieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shield, out var xform))
        {
            if (xform.GridUid != grid || shield.RebootAt != null || shield.Strength <= 0)
                continue;
            if (shield.Radius > 0 && (_xform.GetWorldPosition(xform) - coords.Position).Length() > shield.Radius)
                continue;

            shield.Strength -= damage;
            Spawn("CadiaVoidShieldFlash", coords);
            _audio.PlayPvs(shield.AbsorbSound, uid);

            if (shield.Strength <= 0)
            {
                shield.Strength = 0;
                shield.RebootAt = _timing.CurTime + shield.RebootTime;
                _appearance.SetData(uid, CadiaVoidShieldVisuals.Up, false);
                var collapse = _audio.PlayPvs(shield.CollapseSound, uid, AudioParams.Default.WithVolume(6));
                _audio.SetGridAudio(collapse);
                PopupGrid(grid, Loc.GetString("cadia-void-shield-collapse"), PopupType.LargeCaution);
            }
            Dirty(uid, shield);
            return true;
        }
        return false;
    }

    private void UpdateShields(float frameTime, TimeSpan now)
    {
        var query = EntityQueryEnumerator<CadiaVoidShieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shield, out var xform))
        {
            if (shield.RebootAt is { } reboot)
            {
                if (now < reboot)
                    continue;
                shield.RebootAt = null;
                shield.Strength = shield.MaxStrength * 0.5f;
                _appearance.SetData(uid, CadiaVoidShieldVisuals.Up, true);
                _audio.PlayPvs(shield.RebootSound, uid);
                if (xform.GridUid is { } grid)
                    PopupGrid(grid, Loc.GetString("cadia-void-shield-reboot"), PopupType.Large);
                Dirty(uid, shield);
                continue;
            }

            if (shield.Strength < shield.MaxStrength)
            {
                shield.Strength = Math.Min(shield.MaxStrength, shield.Strength + shield.Regen * frameTime);
                // Синхронизируем раз в секунду, а не каждый кадр.
                if ((int) (now.TotalSeconds) != (int) ((now.TotalSeconds) - frameTime))
                    Dirty(uid, shield);
            }
        }
    }

    private void OnShieldExamined(Entity<CadiaVoidShieldComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.RebootAt is { } reboot)
        {
            args.PushMarkup(Loc.GetString("cadia-void-shield-examine-down", ("seconds", (int) Math.Max(0, (reboot - _timing.CurTime).TotalSeconds))));
            return;
        }
        args.PushMarkup(Loc.GetString("cadia-void-shield-examine", ("percent", (int) (ent.Comp.Strength / ent.Comp.MaxStrength * 100))));
    }

    /// <summary>Чей шаттл: сторона большинства живых бойцов на борту; пустой шаттл ничей.</summary>
    public CadiaFaction? ShipFaction(EntityUid grid)
    {
        var imperium = 0;
        var chaos = 0;
        var members = EntityQueryEnumerator<CadiaFactionMemberComponent, TransformComponent>();
        while (members.MoveNext(out var uid, out var member, out var xform))
        {
            if (xform.GridUid != grid || member.Role == null || _mobState.IsDead(uid))
                continue;
            if (member.Faction == CadiaFaction.Imperium)
                imperium++;
            else
                chaos++;
        }
        if (imperium == 0 && chaos == 0)
            return null;
        return imperium >= chaos ? CadiaFaction.Imperium : CadiaFaction.Chaos;
    }

    // ═══════════════════════ Общее ═══════════════════════

    public void ShakeGrid(EntityUid grid, float strength)
    {
        var filter = Filter.Empty().AddWhereAttachedEntity(e => Transform(e).GridUid == grid);
        foreach (var session in filter.Recipients)
        {
            if (session.AttachedEntity is { } mob)
                _recoil.KickCamera(mob, _random.NextVector2(strength * 0.5f, strength));
        }
    }

    public void PopupGrid(EntityUid grid, string text, PopupType type)
    {
        var filter = Filter.Empty().AddWhereAttachedEntity(e => Transform(e).GridUid == grid);
        foreach (var session in filter.Recipients)
        {
            if (session.AttachedEntity is { } mob)
                _popup.PopupEntity(text, mob, mob, type);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        UpdateStrikes(now);
        UpdateShields(frameTime, now);

if (now >= _nextUi)
        {
            _nextUi = now + UiRate;
            var batteries = EntityQueryEnumerator<CadiaOrbitalBatteryComponent>();
            while (batteries.MoveNext(out var uid, out var battery))
            {
                if (_ui.IsUiOpen(uid, CadiaBatteryUiKey.Key))
                    UpdateUi((uid, battery));
            }
        }
    }
}