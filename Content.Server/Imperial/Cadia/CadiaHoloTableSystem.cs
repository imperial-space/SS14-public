using System.Linq;
using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.Shuttles.Components;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Гололитические столы. У каждой стороны общая голокарта: линии, стрелки, обводки, значки и подписи по слоям
/// «штурм / оборона / разведка» на любом этаже поля. Всё нанесённое сразу видят все столы стороны.
/// Империум ставит чертежи обороны: в мире появляется голограмма, сапёр подаёт сталь и строит по ней.
/// Отметки «цель» видят огневые посты орбитальных батарей своей стороны.
/// </summary>
public sealed class CadiaHoloTableSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly CadiaFlightSystem _flight = default!;
    [Dependency] private readonly CadiaSquadSystem _squads = default!;

    private static readonly SoundSpecifier DrawSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg", AudioParams.Default.WithVolume(-4));
    private static readonly SoundSpecifier EraseSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg", AudioParams.Default.WithVolume(-4));
    private static readonly SoundSpecifier BlueprintSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");
    private static readonly SoundSpecifier BuildSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");
    private static readonly SoundSpecifier BuiltSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    /// <summary>Чертёж: прототип постройки, цена в листах стали и время стройки.</summary>
    public static readonly Dictionary<CadiaBlueprintKind, (string Proto, int Cost, float Time)> Blueprints = new()
    {
        [CadiaBlueprintKind.Sandbags] = ("CadiaSandbags", 2, 3f),
        [CadiaBlueprintKind.Wall] = ("CadiaWall", 4, 6f),
        [CadiaBlueprintKind.TankTrap] = ("CadiaTankTrap", 3, 4f),
        [CadiaBlueprintKind.RazorWire] = ("CadiaRazorWire", 1, 2f),
        [CadiaBlueprintKind.Hydra] = ("CadiaHydraFlak", 25, 20f),
        [CadiaBlueprintKind.Mine] = ("LandMineExplosive", 2, 3f),
        [CadiaBlueprintKind.Pillbox] = ("CadiaPillbox", 40, 25f),
        [CadiaBlueprintKind.AtGun] = ("CadiaAtGun", 15, 10f),
        [CadiaBlueprintKind.BolterNest] = ("CadiaBolterNest", 12, 8f),
        [CadiaBlueprintKind.Stakes] = ("CadiaStakes", 1, 2f),
        [CadiaBlueprintKind.AtMine] = ("CadiaAtMine", 3, 3f),
        [CadiaBlueprintKind.ChaosAltar] = ("CadiaChaosAltar", 20, 12f),
        [CadiaBlueprintKind.SpikeBarricade] = ("CadiaSpikeBarricade", 2, 3f),
        [CadiaBlueprintKind.WarpTrap] = ("CadiaWarpTrap", 2, 2f),
    };

    private const int MaxElements = 400;
    private const int MaxWindow = 320;
    private static readonly TimeSpan LiveRate = TimeSpan.FromSeconds(1);

    private readonly Dictionary<CadiaFaction, List<CadiaHoloElement>> _elements = new()
    {
        [CadiaFaction.Imperium] = new(),
        [CadiaFaction.Chaos] = new(),
    };
    private int _nextId = 1;
    private TimeSpan _nextLive;
    private readonly HashSet<EntityUid> _static = new();
    private const string ProjectionProto = "CadiaHoloProjection";
    private readonly Dictionary<EntityUid, EntityUid> _projections = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<CadiaHoloTableComponent>(CadiaHoloUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<BoundUIClosedEvent>(OnClosed);
            subs.Event<CadiaHoloRequestTilesMessage>(OnRequestTiles);
            subs.Event<CadiaHoloAddMessage>(OnAdd);
            subs.Event<CadiaHoloEraseMessage>(OnErase);
            subs.Event<CadiaHoloUndoMessage>(OnUndo);
            subs.Event<CadiaHoloClearMessage>(OnClear);
        });

        SubscribeLocalEvent<CadiaHoloTableComponent, CadiaHoloVisorActionEvent>(OnVisorAction);
        SubscribeLocalEvent<CadiaBlueprintComponent, InteractUsingEvent>(OnBlueprintInteract);
        SubscribeLocalEvent<CadiaBlueprintComponent, CadiaBlueprintBuildDoAfterEvent>(OnBlueprintBuilt);
        SubscribeLocalEvent<CadiaBlueprintComponent, ExaminedEvent>(OnBlueprintExamined);
    }

    /// <summary>Предвидение псайкера: метка врага на голокарте стороны, на слое разведданных.</summary>
    public void MarkEnemy(CadiaFaction faction, Robust.Shared.Map.MapId map, System.Numerics.Vector2 position, string author)
    {
        var level = LevelOf(_map.GetMap(map)) ?? CadiaFlightLevel.Surface;
        AddSystemElement(faction, new CadiaHoloElement
        {
            Shape = CadiaHoloShape.Marker,
            Layer = CadiaHoloLayer.Intel,
            Level = level,
            Color = Color.FromHex("#ff4a3a"),
            A = position,
            B = position,
            Kind = 1,
        }, author);
    }

    /// <summary>Элемент от имени системы (приказ отряду): виден всем столам стороны.</summary>
    public int AddSystemElement(CadiaFaction faction, CadiaHoloElement element, string author)
    {
        element.Id = _nextId++;
        element.Author = author;
        _elements[faction].Add(element);
        UpdateFaction(faction);
        return element.Id;
    }

    public void RemoveElement(CadiaFaction faction, int id)
    {
        if (_elements[faction].FirstOrDefault(e => e.Id == id) is { } element)
            Remove(faction, element);
        UpdateFaction(faction);
    }

    public void RefreshFaction(CadiaFaction faction) => UpdateFaction(faction);

    /// <summary>Отметки «цель» стороны на поверхности — для огневых постов батарей.</summary>
    public IEnumerable<Vector2> GetTargets(CadiaFaction faction)
    {
        return _elements[faction]
            .Where(e => e.Shape == CadiaHoloShape.Marker && e.Kind == (byte) CadiaHoloMarkerType.Target && e.Level == CadiaFlightLevel.Surface)
            .Select(e => e.A);
    }

    /// <summary>Визор командира: голокарта стороны прямо перед глазом, без правки.</summary>
    private void OnVisorAction(Entity<CadiaHoloTableComponent> ent, ref CadiaHoloVisorActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        if (_ui.IsUiOpen(ent.Owner, CadiaHoloUiKey.Key, args.Performer))
            _ui.CloseUi(ent.Owner, CadiaHoloUiKey.Key, args.Performer);
        else
            _ui.TryOpenUi(ent.Owner, CadiaHoloUiKey.Key, args.Performer);
    }

    // ═══════════════════════ Этажи поля ═══════════════════════

    private EntityUid? LevelMap(EntityUid table, CadiaFlightLevel level)
    {
        if (Transform(table).MapUid is not { } map)
            return null;
        if (!TryComp<CadiaFlightLevelComponent>(map, out var own))
            return level == CadiaFlightLevel.Surface ? map : null;
        return _flight.GetLevelMap(map, (int) level - (int) own.Level);
    }

    private List<CadiaFlightLevel> FieldLevels(EntityUid table)
    {
        var result = new List<CadiaFlightLevel>();
        foreach (var level in Enum.GetValues<CadiaFlightLevel>())
        {
            if (LevelMap(table, level) != null)
                result.Add(level);
        }
        return result;
    }

    private CadiaFlightLevel? LevelOf(EntityUid? map)
    {
        return map != null && TryComp<CadiaFlightLevelComponent>(map, out var level) ? level.Level : null;
    }

    // ═══════════════════════ Интерфейс ═══════════════════════

    private void OnClosed(Entity<CadiaHoloTableComponent> ent, ref BoundUIClosedEvent args)
    {
        if (_projections.Remove(args.Actor, out var projection))
            QueueDel(projection);
    }

    /// <summary>Визор: карта повисает голограммой перед владельцем. Смещение локальное — она поворачивается вместе с ним.</summary>
    private void ShowProjection(EntityUid actor)
    {
        if (_projections.ContainsKey(actor))
            return;
        _projections[actor] = Spawn(ProjectionProto, new Robust.Shared.Map.EntityCoordinates(actor, new System.Numerics.Vector2(0f, -0.55f)));
    }

    private void OnOpened(Entity<CadiaHoloTableComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (ent.Comp.ReadOnly)
            ShowProjection(args.Actor);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scanning.ogg", AudioParams.Default.WithVolume(-6)), ent);
        UpdateUi(ent);
        SendTiles(ent, args.Actor, LevelOf(Transform(ent).MapUid) ?? CadiaFlightLevel.Surface);
    }

    private void OnRequestTiles(Entity<CadiaHoloTableComponent> ent, ref CadiaHoloRequestTilesMessage args)
    {
        SendTiles(ent, args.Actor, args.Level);
    }

    /// <summary>
    /// Плитки этажа в окне: вся планета, если она не больше окна, иначе квадрат вокруг стола.
    /// Сетки этажа (шаттлы, бункеры) тоже ложатся на карту; стены и постройки — ярче, двери — отдельно.
    /// </summary>
    private void SendTiles(Entity<CadiaHoloTableComponent> table, EntityUid actor, CadiaFlightLevel level)
    {
        if (LevelMap(table, level) is not { } map)
            return;
        var mapId = Comp<MapComponent>(map).MapId;
        var center = _xform.GetWorldPosition(table);

        Box2i window;
        if (TryComp<MapGridComponent>(map, out var planet) && planet.LocalAABB.Width <= MaxWindow && planet.LocalAABB.Height <= MaxWindow && planet.LocalAABB.Width > 0)
        {
            var aabb = planet.LocalAABB.Enlarged(4);
            window = new Box2i((int) Math.Floor(aabb.Left), (int) Math.Floor(aabb.Bottom), (int) Math.Ceiling(aabb.Right), (int) Math.Ceiling(aabb.Top));
        }
        else
        {
            var half = MaxWindow / 2;
            var c = new Vector2i((int) center.X, (int) center.Y);
            window = new Box2i(c.X - half, c.Y - half, c.X + half, c.Y + half);
        }

        var width = window.Width;
        var height = window.Height;
        var tiles = new byte[width * height];
        var box = new Box2(window.BottomLeft, window.TopRight);

        var grids = new List<Entity<MapGridComponent>>();
        _mapManager.FindGridsIntersecting(mapId, box, ref grids, includeMap: true);
        foreach (var grid in grids)
        {
            var matrix = _xform.GetWorldMatrix(grid);
            foreach (var tile in _map.GetTilesIntersecting(grid, grid.Comp, box))
            {
                var world = Vector2.Transform((Vector2) tile.GridIndices + new Vector2(0.5f), matrix);
                var x = (int) Math.Floor(world.X) - window.Left;
                var y = (int) Math.Floor(world.Y) - window.Bottom;
                if (x >= 0 && y >= 0 && x < width && y < height)
                    tiles[y * width + x] = 1;
            }
        }

        _static.Clear();
        _lookup.GetEntitiesIntersecting(mapId, box, _static, LookupFlags.Static);
        foreach (var uid in _static)
        {
            var kind = HasComp<DoorComponent>(uid) ? (byte) 3 : IsSolid(uid) ? (byte) 2 : (byte) 0;
            if (kind == 0)
                continue;
            var pos = _xform.GetWorldPosition(uid);
            var x = (int) Math.Floor(pos.X) - window.Left;
            var y = (int) Math.Floor(pos.Y) - window.Bottom;
            if (x >= 0 && y >= 0 && x < width && y < height)
                tiles[y * width + x] = kind;
        }

        _ui.ServerSendUiMessage(table.Owner, CadiaHoloUiKey.Key, new CadiaHoloTilesMessage
        {
            Level = level,
            Origin = window.BottomLeft,
            Width = width,
            Height = height,
            Tiles = tiles,
        }, actor);
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

    private void UpdateUi(Entity<CadiaHoloTableComponent> table)
    {
        var state = new CadiaHoloTableState
        {
            Faction = table.Comp.Faction,
            Planner = table.Comp.Planner && !table.Comp.ReadOnly,
            ReadOnly = table.Comp.ReadOnly,
            TablePosition = _xform.GetWorldPosition(table),
            Levels = FieldLevels(table),
            Elements = _elements[table.Comp.Faction].ToList(),
            Live = Live(table),
            Squads = _squads.HoloSquads(table.Comp.Faction).ToList(),
        };

        // Право очистки клиент определяет сам по роли игрока; сервер всё равно проверяет его при очистке.
        _ui.SetUiState(table.Owner, CadiaHoloUiKey.Key, state);
    }

    private void UpdateFaction(CadiaFaction faction)
    {
        var query = EntityQueryEnumerator<CadiaHoloTableComponent>();
        while (query.MoveNext(out var uid, out var table))
        {
            if (table.Faction == faction && _ui.IsUiOpen(uid, CadiaHoloUiKey.Key))
                UpdateUi((uid, table));
        }
    }

    /// <summary>Живые отметки: свои бойцы и командиры, пилоны, цитадель, шаттлы, свои зенитки, удары, контакты ауспекса.</summary>
    private List<CadiaHoloLive> Live(Entity<CadiaHoloTableComponent> table)
    {
        var result = new List<CadiaHoloLive>();
        var levels = new Dictionary<EntityUid, CadiaFlightLevel>();
        foreach (var level in Enum.GetValues<CadiaFlightLevel>())
        {
            if (LevelMap(table, level) is { } map)
                levels[map] = level;
        }

        var members = EntityQueryEnumerator<CadiaFactionMemberComponent, TransformComponent>();
        while (members.MoveNext(out var uid, out var member, out var xform))
        {
            if (member.Role is not { } role || member.Faction != table.Comp.Faction || _mobState.IsDead(uid) ||
                xform.MapUid is not { } map || !levels.TryGetValue(map, out var lvl))
                continue;
            var commander = _proto.TryIndex(role, out var proto) && proto.Commander;
            result.Add(new CadiaHoloLive(commander ? CadiaHoloLiveKind.Commander : CadiaHoloLiveKind.Friendly, _xform.GetWorldPosition(xform), lvl, _squads.SquadOf(uid)));
        }

        void Add<T>(CadiaHoloLiveKind kind, Func<EntityUid, T, bool>? filter = null) where T : IComponent
        {
            var query = EntityQueryEnumerator<T, TransformComponent>();
            while (query.MoveNext(out var uid, out var comp, out var xform))
            {
                if (xform.MapUid is not { } map || !levels.TryGetValue(map, out var lvl) || (filter != null && !filter(uid, comp)))
                    continue;
                result.Add(new CadiaHoloLive(kind, _xform.GetWorldPosition(xform), lvl));
            }
        }

        Add<CadiaPylonComponent>(CadiaHoloLiveKind.Pylon);
        Add<CadiaCitadelComponent>(CadiaHoloLiveKind.Citadel);
        Add<CadiaEvacBeaconComponent>(CadiaHoloLiveKind.Beacon);

        var sectors = EntityQueryEnumerator<CadiaSectorComponent, TransformComponent>();
        while (sectors.MoveNext(out _, out var sector, out var sectorXform))
        {
            if (sectorXform.MapUid is not { } map || !levels.TryGetValue(map, out var lvl))
                continue;
            var kind = sector.Owner switch
            {
                CadiaFaction.Imperium => CadiaHoloLiveKind.SectorImperium,
                CadiaFaction.Chaos => CadiaHoloLiveKind.SectorChaos,
                _ => CadiaHoloLiveKind.SectorNeutral,
            };
            result.Add(new CadiaHoloLive(kind, _xform.GetWorldPosition(sectorXform), lvl, Label: sector.Letter, Radius: sector.Radius));
        }
        Add<CadiaOrbitalStrikeComponent>(CadiaHoloLiveKind.Strike);
        Add<CadiaAntiAirComponent>(CadiaHoloLiveKind.AntiAir, (_, aa) => aa.Faction == table.Comp.Faction);

        var shuttles = EntityQueryEnumerator<ShuttleComponent, MapGridComponent, TransformComponent>();
        while (shuttles.MoveNext(out var uid, out _, out var grid, out var xform))
        {
            if (xform.MapUid is not { } map || map == uid || !levels.TryGetValue(map, out var lvl))
                continue;
            result.Add(new CadiaHoloLive(CadiaHoloLiveKind.Shuttle, Vector2.Transform(grid.LocalAABB.Center, _xform.GetWorldMatrix(xform)), lvl));
        }

        // Разведданные: вражеские борта, которые видит ауспекс своей стороны.
        var auspexes = EntityQueryEnumerator<CadiaAuspexComponent>();
        while (auspexes.MoveNext(out _, out var auspex))
        {
            if (auspex.Faction != table.Comp.Faction)
                continue;
            foreach (var contact in auspex.Contacts)
            {
                if (TerminatingOrDeleted(contact) || Transform(contact).MapUid is not { } map || !levels.TryGetValue(map, out var lvl))
                    continue;
                result.Add(new CadiaHoloLive(CadiaHoloLiveKind.EnemyContact, _xform.GetWorldPosition(contact), lvl));
            }
        }
        return result;
    }

    private bool IsCommander(EntityUid user)
    {
        return TryComp<CadiaFactionMemberComponent>(user, out var member) && member.Role is { } role &&
               _proto.TryIndex(role, out var proto) && proto.Commander;
    }

    // ═══════════════════════ Правка ═══════════════════════

    private void OnAdd(Entity<CadiaHoloTableComponent> ent, ref CadiaHoloAddMessage args)
    {
        if (ent.Comp.ReadOnly)
            return;
        var list = _elements[ent.Comp.Faction];
        var user = args.Actor;
        if (list.Count >= MaxElements)
        {
            Deny(ent, user, "cadia-holo-full");
            return;
        }

        var element = args.Element;
        element.Id = _nextId++;
        element.Author = Name(user);
        element.AuthorId = TryComp<ActorComponent>(user, out var actor) ? actor.PlayerSession.UserId.UserId : Guid.Empty;
        element.Done = false;
        if (element.Text.Length > 48)
            element.Text = element.Text[..48];
        if (LevelMap(ent, element.Level) == null)
            return;

        if (element.Shape == CadiaHoloShape.Blueprint)
        {
            if (!ent.Comp.Planner || !TryPlaceBlueprint(ent, user, element))
                return;
        }

        list.Add(element);
        _audio.PlayPvs(element.Shape == CadiaHoloShape.Blueprint ? BlueprintSound : DrawSound, ent);
        UpdateFaction(ent.Comp.Faction);
    }

    /// <summary>Голограмма чертежа на месте в мире. Занятое место — отказ.</summary>
    private bool TryPlaceBlueprint(Entity<CadiaHoloTableComponent> table, EntityUid user, CadiaHoloElement element)
    {
        if (!Enum.IsDefined(typeof(CadiaBlueprintKind), element.Kind) || LevelMap(table, element.Level) is not { } map)
            return false;
        var kind = (CadiaBlueprintKind) element.Kind;
        if (!CadiaBlueprintRules.Allowed(kind, table.Comp.Faction))
            return false;
        var (proto, cost, _) = Blueprints[kind];

        var tile = new Vector2((float) Math.Floor(element.A.X), (float) Math.Floor(element.A.Y));
        element.A = tile + new Vector2(0.5f);
        element.Layer = CadiaHoloLayer.Defense;

        var mapId = Comp<MapComponent>(map).MapId;
        var size = kind switch { CadiaBlueprintKind.Hydra => 4, CadiaBlueprintKind.Pillbox => 3, CadiaBlueprintKind.ChaosAltar => 2, _ => 1 };
        var origin = size > 1 ? tile - new Vector2(1, 1) : tile;
        var area = new Box2(origin + new Vector2(0.05f), origin + new Vector2(size - 0.05f));
        if (!_mapManager.TryFindGridAt(new MapCoordinates(element.A, mapId), out _, out _))
        {
            Deny(table, user, "cadia-holo-blueprint-no-ground");
            return false;
        }
        foreach (var uid in _lookup.GetEntitiesIntersecting(mapId, area, LookupFlags.Static | LookupFlags.Sensors))
        {
            if (IsSolid(uid) || HasComp<CadiaBlueprintComponent>(uid))
            {
                Deny(table, user, "cadia-holo-blueprint-occupied");
                return false;
            }
        }

        var ghost = Spawn("CadiaBlueprintGhost", new MapCoordinates(element.A, mapId));
        _xform.AttachToGridOrMap(ghost);
        var comp = EnsureComp<CadiaBlueprintComponent>(ghost);
        comp.Kind = kind;
        comp.Prototype = proto;
        comp.Faction = table.Comp.Faction;
        comp.ElementId = element.Id;
        comp.Cost = cost;
        Dirty(ghost, comp);
        _audio.PlayPvs(BlueprintSound, ghost);
        return true;
    }

    private void OnErase(Entity<CadiaHoloTableComponent> ent, ref CadiaHoloEraseMessage args)
    {
        if (ent.Comp.ReadOnly)
            return;
        var list = _elements[ent.Comp.Faction];
        var eraseId = args.Id;
        var element = list.FirstOrDefault(e => e.Id == eraseId);
        if (element == null)
            return;
        if (!CanEdit(args.Actor, element))
        {
            Deny(ent, args.Actor, "cadia-holo-not-yours");
            return;
        }
        Remove(ent.Comp.Faction, element);
        _audio.PlayPvs(EraseSound, ent);
        UpdateFaction(ent.Comp.Faction);
    }

    private void OnUndo(Entity<CadiaHoloTableComponent> ent, ref CadiaHoloUndoMessage args)
    {
        if (ent.Comp.ReadOnly)
            return;
        var id = TryComp<ActorComponent>(args.Actor, out var actor) ? actor.PlayerSession.UserId.UserId : Guid.Empty;
        var element = _elements[ent.Comp.Faction].LastOrDefault(e => e.AuthorId == id && !e.Done);
        if (element == null)
            return;
        Remove(ent.Comp.Faction, element);
        _audio.PlayPvs(EraseSound, ent);
        UpdateFaction(ent.Comp.Faction);
    }

    private void OnClear(Entity<CadiaHoloTableComponent> ent, ref CadiaHoloClearMessage args)
    {
        if (ent.Comp.ReadOnly)
            return;
        if (!IsCommander(args.Actor))
        {
            Deny(ent, args.Actor, "cadia-holo-commander-only");
            return;
        }
        var layer = args.Layer;
        foreach (var element in _elements[ent.Comp.Faction].Where(e => e.Layer == layer).ToList())
            Remove(ent.Comp.Faction, element);
        _audio.PlayPvs(EraseSound, ent);
        UpdateFaction(ent.Comp.Faction);
    }

    private bool CanEdit(EntityUid user, CadiaHoloElement element)
    {
        var id = TryComp<ActorComponent>(user, out var actor) ? actor.PlayerSession.UserId.UserId : Guid.Empty;
        return element.AuthorId == id || IsCommander(user);
    }

    /// <summary>Стереть элемент; невыстроенный чертёж уносит с собой голограмму.</summary>
    private void Remove(CadiaFaction faction, CadiaHoloElement element)
    {
        _elements[faction].Remove(element);
        if (element.Shape != CadiaHoloShape.Blueprint || element.Done)
            return;
        var ghosts = EntityQueryEnumerator<CadiaBlueprintComponent>();
        while (ghosts.MoveNext(out var uid, out var bp))
        {
            if (bp.ElementId == element.Id && bp.Faction == faction)
                QueueDel(uid);
        }
    }

    private void Deny(EntityUid table, EntityUid user, string key)
    {
        _popup.PopupEntity(Loc.GetString(key), user, user, PopupType.SmallCaution);
        _audio.PlayEntity(DenySound, Filter.Entities(user), table, false);
    }

    // ═══════════════════════ Стройка по чертежу ═══════════════════════

    private void OnBlueprintExamined(Entity<CadiaBlueprintComponent> ent, ref ExaminedEvent args)
    {
        var name = _proto.TryIndex<EntityPrototype>(ent.Comp.Prototype, out var proto) ? proto.Name : ent.Comp.Prototype;
        args.PushMarkup(Loc.GetString("cadia-blueprint-examine", ("name", name), ("cost", ent.Comp.Cost)));
    }

    private void OnBlueprintInteract(Entity<CadiaBlueprintComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<StackComponent>(args.Used, out var stack) || stack.StackTypeId != "Steel")
            return;
        args.Handled = true;

        if (stack.Count < ent.Comp.Cost)
        {
            _popup.PopupEntity(Loc.GetString("cadia-blueprint-need", ("cost", ent.Comp.Cost)), ent, args.User);
            return;
        }

        var time = Blueprints[ent.Comp.Kind].Time;
        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(time), new CadiaBlueprintBuildDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        if (_doAfter.TryStartDoAfter(doAfter))
            _audio.PlayPvs(BuildSound, ent);
    }

    private void OnBlueprintBuilt(Entity<CadiaBlueprintComponent> ent, ref CadiaBlueprintBuildDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used || TerminatingOrDeleted(ent))
            return;
        args.Handled = true;

        if (!TryComp<StackComponent>(used, out var stack) || !_stack.TryUse((used, stack), ent.Comp.Cost))
        {
            _popup.PopupEntity(Loc.GetString("cadia-blueprint-need", ("cost", ent.Comp.Cost)), ent, args.User);
            return;
        }

        var coords = Transform(ent).Coordinates;
        var built = Spawn(ent.Comp.Prototype, coords);
        _xform.AttachToGridOrMap(built);
        var builtXform = Transform(built);
        if (!builtXform.Anchored)
            _xform.AnchorEntity(built, builtXform);
        _audio.PlayPvs(BuiltSound, built);
        _popup.PopupEntity(Loc.GetString("cadia-blueprint-built"), built, args.User);

        var element = _elements[ent.Comp.Faction].FirstOrDefault(e => e.Id == ent.Comp.ElementId);
        if (element != null)
            element.Done = true;
        QueueDel(ent);
        UpdateFaction(ent.Comp.Faction);
    }

    // ═══════════════════════ Обновление ═══════════════════════

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextLive)
            return;
        _nextLive = now + LiveRate;

        var query = EntityQueryEnumerator<CadiaHoloTableComponent>();
        while (query.MoveNext(out var uid, out var table))
        {
            if (_ui.IsUiOpen(uid, CadiaHoloUiKey.Key))
                UpdateUi((uid, table));
        }
    }
}
