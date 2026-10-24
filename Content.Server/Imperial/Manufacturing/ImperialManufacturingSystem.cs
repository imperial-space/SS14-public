using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Shared.Chasm;
using Content.Shared.Conveyor;
using Content.Shared.Examine;
using Content.Shared.Imperial.Manufacturing;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Server.Power.Components;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Manufacturing;

/// <summary>
/// Производственные линии TG (code/modules/manufactorio): машины принимают предметы с конвейера
/// или от соседней машины и выдают результат по направлению. Здесь общая часть: приём, выдача, питание, внешний вид.
/// </summary>
public sealed partial class ImperialManufacturingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private static readonly string Pulsing = "Pulsing";
    private static readonly ProtoId<TagPrototype> OreTag = "Ore";

    private const string Orange = "#FF9900";
    private const string Blue = "#555CC2";

    /// <summary>Холостая нагрузка между действиями, Вт.</summary>
    private const float IdleLoad = 50f;

    private float _bumpAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialManufacturingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImperialManufacturingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialManufacturingComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<ImperialManufacturingComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<ImperialManufacturingComponent, PowerChangedEvent>(OnPower);
        SubscribeLocalEvent<ImperialManufacturingComponent, EntityTerminatingEvent>(OnTerminating);

        InitializeMachines();
        InitializeUi();
    }

    private void OnMapInit(Entity<ImperialManufacturingComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<Container>(ent, ImperialManufacturingComponent.ContainerId);
        _container.EnsureContainer<Container>(ent, ImperialManufacturingComponent.OutContainerId);
        if (ent.Comp.Kind == ImperialManufacturingKind.Router)
            ent.Comp.Directions = new List<Direction> { Direction.North, Direction.South, Direction.East, Direction.West };
        if (ent.Comp.Kind == ImperialManufacturingKind.Sorter)
            ent.Comp.DirIfNotMet = Transform(ent).LocalRotation.GetCardinalDir();

        UpdateVisuals(ent);
    }

    private void OnMove(Entity<ImperialManufacturingComponent> ent, ref MoveEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnAnchor(Entity<ImperialManufacturingComponent> ent, ref AnchorStateChangedEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnPower(Entity<ImperialManufacturingComponent> ent, ref PowerChangedEvent args)
    {
        UpdateVisuals(ent);
    }

    /// <summary>atom_destruction() и dump_inventory_contents(): содержимое и удержанные результаты падают на пол.</summary>
    private void OnTerminating(Entity<ImperialManufacturingComponent> ent, ref EntityTerminatingEvent args)
    {
        foreach (var id in new[] { ImperialManufacturingComponent.ContainerId, ImperialManufacturingComponent.OutContainerId })
        {
            if (_container.TryGetContainer(ent, id, out var container))
                _container.EmptyContainer(container, true);
        }
    }

    /// <summary>Направление, куда смотрит машина (dir).</summary>
    public Direction Dir(EntityUid uid) => Transform(uid).LocalRotation.GetCardinalDir();

    public BaseContainer Contents(EntityUid uid) => _container.EnsureContainer<Container>(uid, ImperialManufacturingComponent.ContainerId);

    public BaseContainer Output(EntityUid uid) => _container.EnsureContainer<Container>(uid, ImperialManufacturingComponent.OutContainerId);

    private bool Powered(EntityUid uid) => !HasComp<ApcPowerReceiverComponent>(uid) || _power.IsPowered(uid);

    /// <summary>add_load(): нагрузка на время действия.</summary>
    private void AddLoad(EntityUid uid, float load)
    {
        if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            _power.SetLoad((uid, receiver), MathF.Max(IdleLoad, load));
    }

    private void Idle(EntityUid uid)
    {
        if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            _power.SetLoad((uid, receiver), IdleLoad);
    }

    #region Клетки

    private bool TryGetTile(EntityUid uid, out EntityUid grid, out MapGridComponent gridComp, out Vector2i tile)
    {
        grid = default;
        gridComp = default!;
        tile = default;
        var xform = Transform(uid);
        if (xform.GridUid is not { } g || !TryComp(g, out MapGridComponent? gc))
            return false;

        grid = g;
        gridComp = gc;
        tile = _map.TileIndicesFor(g, gc, xform.Coordinates);
        return true;
    }

    /// <summary>get_overfloor_objects(): незакреплённые предметы на клетке (без мобов и самой машины).</summary>
    private List<EntityUid> OverfloorObjects(EntityUid grid, MapGridComponent gridComp, Vector2i tile, EntityUid? except = null)
    {
        var result = new List<EntityUid>();
        foreach (var uid in _lookup.GetLocalEntitiesIntersecting(grid, tile, 0f, gridComp: gridComp))
        {
            if (uid == except || HasComp<MobStateComponent>(uid) || Transform(uid).Anchored)
                continue;
            result.Add(uid);
        }

        return result;
    }

    private Entity<ImperialManufacturingComponent>? MachineAt(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        foreach (var uid in _map.GetAnchoredEntities(grid, gridComp, tile))
        {
            if (TryComp<ImperialManufacturingComponent>(uid, out var comp))
                return (uid, comp);
        }

        return null;
    }

    #endregion

    #region Приём и выдача

    /// <summary>
    /// receive_resource(). receiveDir — сторона машины, с которой пришёл предмет.
    /// </summary>
    public bool Receive(Entity<ImperialManufacturingComponent> ent, EntityUid item, EntityUid? from, Direction receiveDir)
    {
        if (TerminatingOrDeleted(item))
            return false;

        return ent.Comp.Kind switch
        {
            ImperialManufacturingKind.Unloader => ReceiveUnloader(ent, item, receiveDir),
            ImperialManufacturingKind.Crusher => ReceiveCrusher(ent, item, receiveDir),
            ImperialManufacturingKind.Smelter => ReceiveSmelter(ent, item, receiveDir),
            ImperialManufacturingKind.Lathe => ReceiveLathe(ent, item, receiveDir),
            ImperialManufacturingKind.Crafter => ReceiveOntoTile(ent, item),
            ImperialManufacturingKind.Router => ReceiveRouter(ent, item, from, receiveDir),
            ImperialManufacturingKind.Sorter => ReceiveOntoTile(ent, item),
            ImperialManufacturingKind.StorageBox => ReceiveStorage(ent, item),
            _ => false,
        };
    }

    /// <summary>
    /// send_resource(): отдать предмет соседней машине или выложить на соседнюю клетку.
    /// item == null — только проверка, можно ли выдать.
    /// </summary>
    public bool Send(EntityUid sender, EntityUid? item, Direction dir)
    {
        if (!TryGetTile(sender, out var grid, out var gridComp, out var tile))
            return false;

        var next = tile + dir.ToIntVec();
        if (MachineAt(grid, gridComp, next) is { } machine)
        {
            if (item == null)
                return true;

            return Transform(machine).Anchored && Receive(machine, item.Value, sender, dir.GetOpposite());
        }

        if (_map.TryGetTileRef(grid, gridComp, next, out var tileRef) && !tileRef.Tile.IsEmpty
            && _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable) && !IsChasm(grid, gridComp, next))
            return false;

        if (OverfloorObjects(grid, gridComp, next).Count >= ImperialManufacturingComponent.TurfLagLimit)
            return false;

        if (item == null)
            return true;

        _container.TryRemoveFromContainer(item.Value, true);
        var coords = _map.GridTileToLocal(grid, gridComp, next);
        _transform.SetCoordinates(item.Value, coords.Offset(new Vector2(_random.NextFloat(-0.15f, 0.15f), _random.NextFloat(-0.15f, 0.15f))));
        return true;
    }

    private bool IsChasm(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        return _map.GetAnchoredEntities(grid, gridComp, tile).Any(HasComp<ChasmComponent>);
    }

    /// <summary>Положить предмет внутрь машины.</summary>
    private bool Store(EntityUid machine, EntityUid item)
    {
        _container.TryRemoveFromContainer(item, true);
        return _container.Insert(item, Contents(machine));
    }

    /// <summary>may_merge_in_contents_and_do_so(): долить стопку в такую же внутри.</summary>
    private bool MergeIntoContents(EntityUid machine, EntityUid item)
    {
        if (!TryComp<StackComponent>(item, out var stack))
            return false;

        foreach (var other in Contents(machine).ContainedEntities)
        {
            if (other == item || !TryComp<StackComponent>(other, out var otherStack) || otherStack.StackTypeId != stack.StackTypeId)
                continue;

            if (otherStack.Count + stack.Count > _stack.GetMaxCount(otherStack))
                continue;

            return _stack.TryMergeStacks((item, stack), (other, otherStack), out _);
        }

        return false;
    }

    /// <summary>Сайты crafter и sorter: предмет ложится на клетку машины.</summary>
    private bool ReceiveOntoTile(Entity<ImperialManufacturingComponent> ent, EntityUid item)
    {
        if (!TryGetTile(ent, out var grid, out var gridComp, out var tile))
            return false;

        if (OverfloorObjects(grid, gridComp, tile).Count >= ImperialManufacturingComponent.TurfLagLimit)
            return false;

        _container.TryRemoveFromContainer(item, true);
        _transform.SetCoordinates(item, _map.GridTileToLocal(grid, gridComp, tile));
        return true;
    }

    /// <summary>Bumped(): предмет, упёршийся в машину с работающего конвейера.</summary>
    private void ScanBumps()
    {
        var query = EntityQueryEnumerator<ImperialManufacturingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!xform.Anchored || comp.Kind is ImperialManufacturingKind.Crafter or ImperialManufacturingKind.Sorter)
                continue;
            if (!TryGetTile(uid, out var grid, out var gridComp, out var tile))
                continue;

            var center = _transform.GetWorldPosition(xform);
            foreach (var dir in new[] { Direction.North, Direction.South, Direction.East, Direction.West })
            {
                var side = tile + dir.ToIntVec();
                if (!_map.GetAnchoredEntities(grid, gridComp, side).Any(e => TryComp<ConveyorComponent>(e, out var conveyor) && conveyor.State != ConveyorState.Off))
                    continue;

                foreach (var item in OverfloorObjects(grid, gridComp, side).Concat(Mobs(grid, gridComp, side)).ToList())
                {
                    if ((_transform.GetWorldPosition(item) - center).Length() > 0.9f)
                        continue;
                    if (HasComp<MobStateComponent>(item) && !comp.AllowMobBumpIntake)
                        continue;

                    Receive((uid, comp), item, null, dir);
                }
            }
        }
    }

    private IEnumerable<EntityUid> Mobs(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        foreach (var uid in _lookup.GetLocalEntitiesIntersecting(grid, tile, 0f, gridComp: gridComp))
        {
            if (HasComp<MobStateComponent>(uid))
                yield return uid;
        }
    }

    #endregion

    #region Внешний вид и осмотр

    private static int ByondDir(Direction dir) => dir switch
    {
        Direction.North => 1,
        Direction.South => 2,
        Direction.East => 4,
        Direction.West => 8,
        _ => 2,
    };

    /// <summary>update_overlays(): стрелки входа и выхода, стрелки маршрутизатора.</summary>
    public void UpdateVisuals(Entity<ImperialManufacturingComponent> ent)
    {
        var dir = Dir(ent);
        var io = new StringBuilder();
        void Io(Direction d, string color) => io.Append($"{ByondDir(d)}:{color};");

        switch (ent.Comp.Kind)
        {
            case ImperialManufacturingKind.Crusher:
            case ImperialManufacturingKind.Smelter:
            case ImperialManufacturingKind.Lathe:
                Io(dir, Orange);
                Io(dir.GetOpposite(), Blue);
                break;
            case ImperialManufacturingKind.Unloader:
                Io(dir, Orange);
                Io(dir.GetOpposite(), Blue);
                Io(Turn(dir, ent.Comp.FlipSide ? 90 : -90), Orange);
                break;
            case ImperialManufacturingKind.Router:
            {
                var router = new StringBuilder();
                // Поворот самой машины меняет смещение направленных наложений на клиенте.
                router.Append($"rot{ByondDir(dir)};");
                foreach (var d in new[] { Direction.North, Direction.South, Direction.East, Direction.West })
                {
                    var variant = ent.Comp.DisabledDirs.Contains(d) ? "bl" : d == ent.Comp.InDir ? "in" : "out";
                    router.Append($"{ByondDir(d)}:{variant};");
                }

                _appearance.SetData(ent, ImperialManufacturingVisuals.Router, router.ToString());
                break;
            }
        }

        _appearance.SetData(ent, ImperialManufacturingVisuals.Io, Transform(ent).Anchored ? io.ToString() : string.Empty);
    }

    /// <summary>turn(dir, angle): положительный угол — против часовой стрелки.</summary>
    public static Direction Turn(Direction dir, int degrees)
    {
        var steps = degrees / 45;
        return (Direction) ((((int) dir + steps) % 8 + 8) % 8);
    }

    /// <summary>flick_overlay_view(): «состояние|секунды|направление BYOND|метка».</summary>
    private void Flick(EntityUid uid, string state, TimeSpan duration, Direction dir = Direction.South)
    {
        _appearance.SetData(uid, ImperialManufacturingVisuals.Flick,
            $"{state}|{duration.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{ByondDir(dir)}|{_timing.CurTick.Value}");
    }

    private void OnExamined(Entity<ImperialManufacturingComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Kind is not (ImperialManufacturingKind.StorageBox or ImperialManufacturingKind.Router or ImperialManufacturingKind.Sorter))
            args.PushMarkup(Loc.GetString("imperial-manufacturing-examine-power"));

        var contents = Contents(ent).ContainedEntities;
        args.PushMarkup(Loc.GetString(contents.Count > 0 ? "imperial-manufacturing-examine-contains" : "imperial-manufacturing-examine-empty"));
        foreach (var thing in contents)
        {
            var name = Name(thing);
            if (TryComp<StackComponent>(thing, out var stack))
                name = $"{stack.Count} {name}";
            args.PushMarkup("  " + Robust.Shared.Utility.FormattedMessage.EscapeText(name));
        }

        ExamineMachine(ent, args);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _bumpAccumulator += frameTime;
        if (_bumpAccumulator >= 0.1f)
        {
            _bumpAccumulator = 0;
            ScanBumps();
        }

        UpdateMachines();
    }
}
