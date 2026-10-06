using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Методы для админ-команды «fission».</summary>
public sealed partial class FissionSystem
{
    private static readonly EntProtoId ReactorProto = "ImperialFissionReactor";
    private static readonly EntProtoId ChamberProto = "ImperialFissionChamber";
    private static readonly EntProtoId IntakeProto = "ImperialFissionGasIntake";
    private static readonly EntProtoId ExtractorProto = "ImperialFissionGasExtractor";
    private static readonly EntProtoId ModeratorProto = "ImperialFissionGasModerator";
    private static readonly EntProtoId TerminalProto = "ImperialFissionPowerTerminal";
    private static readonly EntProtoId MonitorProto = "ImperialFissionMonitor";
    private static readonly EntProtoId FuelRod = "ImperialFissionRodUranium238";
    private static readonly EntProtoId ModeratorRod = "ImperialFissionRodHeavyWater";
    private static readonly EntProtoId GuideProto = "ImperialFissionGuide";

    /// <summary>
    /// Ядро 3×3, кольцо из 11 камер, три газовых узла снизу (подача, замедлитель, откачка),
    /// силовой терминал справа и консоль слева.
    /// </summary>
    public EntityUid? SpawnAssembled(EntityCoordinates coordinates)
    {
        if (_xform.GetGrid(coordinates) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return null;

        var grid = new Entity<MapGridComponent>(gridUid, gridComp);
        var center = _map.TileIndicesFor(grid, grid, coordinates);
        var reactor = SpawnAnchored(ReactorProto, grid, center, Direction.South);

        var chambers = new List<Vector2i>();
        for (var x = -2; x <= 2; x++)
            chambers.Add(new Vector2i(x, 2));
        chambers.Add(new Vector2i(-2, 1));
        chambers.Add(new Vector2i(-2, 0));
        chambers.Add(new Vector2i(-2, -1));
        chambers.Add(new Vector2i(-2, -2));
        chambers.Add(new Vector2i(2, 1));
        chambers.Add(new Vector2i(2, 0));

        foreach (var offset in chambers)
            SpawnAnchored(ChamberProto, grid, center + offset, Direction.South);

        // Газовые узлы трубой наружу (вниз), реактор — с противоположной стороны.
        SpawnAnchored(IntakeProto, grid, center + new Vector2i(-1, -2), Direction.South);
        SpawnAnchored(ModeratorProto, grid, center + new Vector2i(0, -2), Direction.South);
        SpawnAnchored(ExtractorProto, grid, center + new Vector2i(1, -2), Direction.South);

        var terminal = SpawnAnchored(TerminalProto, grid, center + new Vector2i(2, -1), Direction.South);
        Comp<FissionPowerTerminalComponent>(terminal).Reactor = reactor;
        EnsureTerminalCable(terminal);
        SpawnAnchored(MonitorProto, grid, center + new Vector2i(-4, 0), Direction.East);
        Spawn(GuideProto, _map.GridTileToLocal(grid, grid, center + new Vector2i(-4, -1)));

        var reactorComp = Comp<FissionReactorComponent>(reactor);
        RebuildNetwork((reactor, reactorComp));
        return reactor;
    }

    private EntityUid SpawnAnchored(EntProtoId proto, Entity<MapGridComponent> grid, Vector2i tile, Direction dir)
    {
        var uid = Spawn(proto, _map.GridTileToLocal(grid, grid, tile));
        var xform = Transform(uid);
        _xform.SetLocalRotation(uid, dir.ToAngle(), xform);
        if (!xform.Anchored)
            _xform.AnchorEntity((uid, xform), grid);
        return uid;
    }

    /// <summary>Вставить стержни (уран-238 через один с тяжёлой водой) и добавить азот в активную зону.</summary>
    public void Fill(EntityUid uid, float nitrogen)
    {
        if (!TryComp<FissionReactorComponent>(uid, out var reactor))
            return;

        RebuildNetwork((uid, reactor));
        var index = 0;
        foreach (var chamberUid in reactor.ConnectedChambers)
        {
            if (!TryComp<FissionChamberComponent>(chamberUid, out var chamber) || GetRod(chamberUid) != null)
                continue;

            var rod = Spawn(index++ % 2 == 0 ? FuelRod : ModeratorRod, Transform(chamberUid).Coordinates);
            var slot = _container.EnsureContainer<Robust.Shared.Containers.ContainerSlot>(chamberUid, FissionChamberComponent.ContainerId);
            _container.Insert(rod, slot);
            chamber.State = FissionChamberState.Down;
            chamber.DurabilityLevel = DurabilityLevel(Comp<FissionRodComponent>(rod));
            SetChamberDensity(chamberUid, false);
            UpdateChamberVisuals((chamberUid, chamber));
        }

        if (nitrogen > 0)
        {
            var add = new GasMixture { Temperature = Atmospherics.T20C };
            add.AdjustMoles(Gas.Nitrogen, nitrogen);
            _atmos.Merge(reactor.Air, add);
        }
    }

    public void SetDesiredPower(EntityUid uid, float power)
    {
        if (TryComp<FissionReactorComponent>(uid, out var reactor) && !reactor.ControlLockout)
            reactor.DesiredPower = Math.Clamp(power, 0, 100);
    }

    public void SetFrozen(EntityUid uid, bool frozen)
    {
        if (TryComp<FissionReactorComponent>(uid, out var reactor))
            reactor.AdminIntervention = frozen;
    }

    public void SetDamage(EntityUid uid, float damage)
    {
        if (TryComp<FissionReactorComponent>(uid, out var reactor))
            AdjustDamage((uid, reactor), damage, true);
    }

    public bool Overload(EntityUid uid)
    {
        return TryComp<FissionReactorComponent>(uid, out var reactor) && OverloadReactor((uid, reactor));
    }

    private float TerminalSupply(EntityUid reactor)
    {
        float total = 0;
        var query = EntityQueryEnumerator<FissionPowerTerminalComponent, Content.Server.Power.Components.PowerSupplierComponent>();
        while (query.MoveNext(out _, out var terminal, out var supplier))
        {
            if (terminal.Reactor == reactor)
                total += supplier.MaxSupply;
        }

        return total;
    }

    /// <summary>Отладка терминалов: закреплён ли, сколько реально отдаёт в сеть и подключён ли к кабелю.</summary>
    private string TerminalDebug(EntityUid reactor)
    {
        var parts = new List<string>();
        var query = EntityQueryEnumerator<FissionPowerTerminalComponent, Content.Server.Power.Components.PowerSupplierComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var terminal, out var supplier, out var xform))
        {
            if (terminal.Reactor != reactor)
                continue;

            var nodes = _nodeContainer.TryGetNode(uid, "output", out Content.Shared.NodeContainer.Node? node) && node.NodeGroup != null
                ? node.NodeGroup.Nodes.Count
                : 0;
            parts.Add($"{ToPrettyString(uid)} anchored={xform.Anchored} max={supplier.MaxSupply:0} current={supplier.CurrentSupply:0} netNodes={nodes}");
        }

        return string.Join("; ", parts);
    }

    public string Status(EntityUid uid)
    {
        if (!TryComp<FissionReactorComponent>(uid, out var reactor))
            return "not a fission reactor";

        var operational = 0;
        foreach (var chamber in reactor.ConnectedChambers)
        {
            if (TryComp<FissionChamberComponent>(chamber, out var comp) && comp.Operational)
                operational++;
        }

        return $"{ToPrettyString(uid)}: offline={reactor.Offline} starting={reactor.StartingUp} broken={reactor.Broken} " +
               $"desired={reactor.DesiredPower:0.#} operating={reactor.OperatingPower:0.#} rods={reactor.ControlRodsRemaining} " +
               $"chambers={reactor.ConnectedChambers.Count} operational={operational} power={reactor.FinalPower / 1000:0.##}kW " +
               $"heat={reactor.FinalHeat:0.##} T={reactor.Air.Temperature:0.##}K P={reactor.Air.Pressure:0.##}kPa " +
               $"mol={reactor.Air.TotalMoles:0.##} moderatorMol={reactor.ModeratorGas.TotalMoles:0.##} " +
               $"reactivity={reactor.Reactivity:0.###} threshold={reactor.HeatDamageThreshold:0.#} " +
               $"integrity={GetIntegrity(reactor)}% venting={reactor.Venting} countdown={reactor.FinalCountdown} " +
               $"override={reactor.SafetyOverride} lockout={reactor.ControlLockout} terminalSupply={TerminalSupply(uid) / 1000:0.##}kW terminals=[{TerminalDebug(uid)}]";
    }
}
