using System.Linq;
using Content.Server.Imperial.MachineParts;
using Content.Shared.Examine;
using Content.Shared.Imperial.MachineParts;
using Content.Shared.Imperial.ModularShield;
using Content.Shared.Interaction;
using Content.Shared.Wires;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.ModularShield;

public sealed partial class ImperialModularShieldSystem
{
    private void InitializeModules()
    {
        SubscribeLocalEvent<ImperialModularShieldModuleComponent, MapInitEvent>(OnModuleMapInit);
        SubscribeLocalEvent<ImperialModularShieldModuleComponent, ComponentShutdown>(OnModuleShutdown);
        SubscribeLocalEvent<ImperialModularShieldModuleComponent, MoveEvent>(OnModuleMove);
        SubscribeLocalEvent<ImperialModularShieldModuleComponent, InteractUsingEvent>(OnModuleInteractUsing);
        SubscribeLocalEvent<ImperialModularShieldModuleComponent, ExaminedEvent>(OnModuleExamined);
        SubscribeLocalEvent<ImperialModularShieldModuleComponent, ImperialMachinePartsRefreshEvent>(OnModuleParts);
    }

    private void OnModuleMapInit(Entity<ImperialModularShieldModuleComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.LastRotation = Transform(ent).LocalRotation;
        TryConnect(ent, null);
    }

    /// <summary>Destroy(): отключиться от генератора и узла.</summary>
    private void OnModuleShutdown(Entity<ImperialModularShieldModuleComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Kind is ImperialModularShieldModuleKind.Node or ImperialModularShieldModuleKind.Cable)
        {
            DisconnectConnectedThroughUs(ent);
            foreach (var connected in ent.Comp.ConnectedThroughUs)
            {
                if (TryComp<ImperialModularShieldModuleComponent>(connected, out var module))
                    module.ConnectedNode = null;
            }
        }

        Disconnect(ent);
    }

    /// <summary>wrench_act(): поворот рвёт связь и ищет новую.</summary>
    private void OnModuleMove(Entity<ImperialModularShieldModuleComponent> ent, ref MoveEvent args)
    {
        var rotation = Transform(ent).LocalRotation;
        if (args.OldPosition == args.NewPosition && rotation.EqualsApprox(ent.Comp.LastRotation))
            return;

        ent.Comp.LastRotation = rotation;
        if (ent.Comp.Kind is ImperialModularShieldModuleKind.Node or ImperialModularShieldModuleKind.Cable)
            DisconnectConnectedThroughUs(ent);

        Disconnect(ent);
        TryConnect(ent, null);
    }

    private void OnModuleInteractUsing(Entity<ImperialModularShieldModuleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, Pulsing))
            return;

        args.Handled = true;
        TryConnect(ent, args.User);
    }

    private void OnModuleExamined(Entity<ImperialModularShieldModuleComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.ShieldGenerator == null && ent.Comp.ConnectedNode == null
            ? "imperial-modular-shield-module-examine-free"
            : "imperial-modular-shield-module-examine-connected"));
    }

    /// <summary>RefreshParts() зарядника, ретранслятора и колодца.</summary>
    private void OnModuleParts(Entity<ImperialModularShieldModuleComponent> ent, ref ImperialMachinePartsRefreshEvent args)
    {
        ent.Comp.Boost = ent.Comp.Kind switch
        {
            ImperialModularShieldModuleKind.Charger => 2 * args.Ratings.Sum(ImperialStockPartType.Servo, 0),
            ImperialModularShieldModuleKind.Relay => 0.25f * args.Ratings.Sum(ImperialStockPartType.MicroLaser, 0),
            ImperialModularShieldModuleKind.Well => 20 * args.Ratings.Sum(ImperialStockPartType.Capacitor, 0),
            _ => 0,
        };

        if (ent.Comp.ShieldGenerator is { } generator && TryComp<ImperialModularShieldGeneratorComponent>(generator, out var gen))
            CalculateBoost((generator, gen));
    }

    private void Disconnect(Entity<ImperialModularShieldModuleComponent> ent)
    {
        if (ent.Comp.ShieldGenerator is { } generator && TryComp<ImperialModularShieldGeneratorComponent>(generator, out var gen))
        {
            gen.ConnectedModules.Remove(ent);
            CalculateBoost((generator, gen));
        }

        ent.Comp.ShieldGenerator = null;

        if (ent.Comp.ConnectedNode is { } node && TryComp<ImperialModularShieldModuleComponent>(node, out var nodeComp))
        {
            nodeComp.ConnectedThroughUs.Remove(ent);
            UpdateModuleVisuals((node, nodeComp));
        }

        ent.Comp.ConnectedNode = null;
        UpdateModuleVisuals(ent);
    }

    /// <summary>Сущность нужного вида на клетке перед модулем.</summary>
    private EntityUid? FindInFront<T>(EntityUid uid, Func<EntityUid, bool>? filter = null) where T : IComponent
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return null;

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates) + xform.LocalRotation.GetCardinalDir().ToIntVec();
        foreach (var other in _map.GetAnchoredEntities(grid, gridComp, tile))
        {
            if (HasComp<T>(other) && (filter == null || filter(other)))
                return other;
        }

        return null;
    }

    /// <summary>try_connect(): генератор или узел на клетке перед модулем.</summary>
    private void TryConnect(Entity<ImperialModularShieldModuleComponent> ent, EntityUid? user)
    {
        if (ent.Comp.ShieldGenerator != null || ent.Comp.ConnectedNode != null)
        {
            Alert(ent, user, "imperial-modular-shield-already-connected");
            UpdateModuleVisuals(ent);
            return;
        }

        if (FindInFront<ImperialModularShieldGeneratorComponent>(ent) is { } generator)
        {
            var gen = Comp<ImperialModularShieldGeneratorComponent>(generator);
            ent.Comp.ShieldGenerator = generator;
            gen.ConnectedModules.Add(ent);
            Alert(ent, user, "imperial-modular-shield-connected-generator");
            UpdateModuleVisuals(ent);
            CalculateBoost((generator, gen));
            AfterNodeConnected(ent);
            return;
        }

        var node = FindInFront<ImperialModularShieldModuleComponent>(ent, other =>
            Comp<ImperialModularShieldModuleComponent>(other).Kind is ImperialModularShieldModuleKind.Node or ImperialModularShieldModuleKind.Cable);
        if (node is { } nodeUid)
        {
            var nodeComp = Comp<ImperialModularShieldModuleComponent>(nodeUid);
            if (!nodeComp.AllowBoosters && ent.Comp.IsBooster)
            {
                Alert(ent, user, "imperial-modular-shield-cant-connect");
                UpdateModuleVisuals(ent);
                return;
            }

            ent.Comp.ConnectedNode = nodeUid;
            nodeComp.ConnectedThroughUs.Add(ent);
            UpdateModuleVisuals((nodeUid, nodeComp));
            ent.Comp.ShieldGenerator = nodeComp.ShieldGenerator;
            if (ent.Comp.ShieldGenerator is { } viaNode && TryComp<ImperialModularShieldGeneratorComponent>(viaNode, out var gen))
            {
                gen.ConnectedModules.Add(ent);
                Alert(ent, user, "imperial-modular-shield-connected-generator");
                UpdateModuleVisuals(ent);
                CalculateBoost((viaNode, gen));
                AfterNodeConnected(ent);
                return;
            }

            UpdateModuleVisuals(ent);
            Alert(ent, user, "imperial-modular-shield-connected-node");
            return;
        }

        UpdateModuleVisuals(ent);
        Alert(ent, user, "imperial-modular-shield-no-connection");
    }

    /// <summary>node/try_connect(): узел подключает к генератору всё, что подключено через него.</summary>
    private void AfterNodeConnected(Entity<ImperialModularShieldModuleComponent> ent)
    {
        if (ent.Comp.Kind is not (ImperialModularShieldModuleKind.Node or ImperialModularShieldModuleKind.Cable) || ent.Comp.ShieldGenerator is not { } generator)
            return;

        ConnectConnectedThroughUs(ent);
        if (TryComp<ImperialModularShieldGeneratorComponent>(generator, out var gen))
            CalculateBoost((generator, gen));
    }

    private void ConnectConnectedThroughUs(Entity<ImperialModularShieldModuleComponent> node)
    {
        if (node.Comp.ShieldGenerator is not { } generator || !TryComp<ImperialModularShieldGeneratorComponent>(generator, out var gen))
            return;

        foreach (var connected in node.Comp.ConnectedThroughUs)
        {
            if (!TryComp<ImperialModularShieldModuleComponent>(connected, out var module))
                continue;

            gen.ConnectedModules.Add(connected);
            module.ShieldGenerator = generator;
            if (module.Kind is ImperialModularShieldModuleKind.Node or ImperialModularShieldModuleKind.Cable)
                ConnectConnectedThroughUs((connected, module));
            UpdateModuleVisuals((connected, module));
        }
    }

    private void DisconnectConnectedThroughUs(Entity<ImperialModularShieldModuleComponent> node)
    {
        TryComp<ImperialModularShieldGeneratorComponent>(node.Comp.ShieldGenerator, out var gen);
        foreach (var connected in node.Comp.ConnectedThroughUs)
        {
            if (!TryComp<ImperialModularShieldModuleComponent>(connected, out var module))
                continue;

            gen?.ConnectedModules.Remove(connected);
            if (module.Kind is ImperialModularShieldModuleKind.Node or ImperialModularShieldModuleKind.Cable)
                DisconnectConnectedThroughUs((connected, module));
            module.ShieldGenerator = null;
            UpdateModuleVisuals((connected, module));
        }

        if (gen != null && node.Comp.ShieldGenerator is { } generator)
            CalculateBoost((generator, gen));
    }

    private void Alert(EntityUid uid, EntityUid? user, string key)
    {
        if (user != null)
            _popup.PopupEntity(Loc.GetString(key), uid, user.Value);
    }

    private void UpdateModules()
    {
        var query = EntityQueryEnumerator<ImperialModularShieldModuleComponent>();
        while (query.MoveNext(out var uid, out var comp))
            UpdateModuleVisuals((uid, comp));
    }

    /// <summary>update_icon_state() модулей; у кабеля — какие соседние узлы подключены.</summary>
    private void UpdateModuleVisuals(Entity<ImperialModularShieldModuleComponent> ent)
    {
        var open = TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open ? "open" : "closed";
        var on = ent.Comp.ShieldGenerator != null && _power.IsPowered(ent.Owner) ? "on" : "off";
        string state;
        if (ent.Comp.Kind == ImperialModularShieldModuleKind.Cable)
        {
            var xform = Transform(ent);
            var dir = xform.LocalRotation.GetCardinalDir();
            bool Connected(Direction side)
            {
                if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
                    return false;

                var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates) + side.ToIntVec();
                return _map.GetAnchoredEntities(grid, gridComp, tile).Any(e => ent.Comp.ConnectedThroughUs.Contains(e));
            }

            // turn(dir, 270) — справа, 180 — сзади, 90 — слева (против часовой в BYOND).
            var right = Connected(Rotate(dir, -2));
            var back = Connected(Rotate(dir, 4));
            var left = Connected(Rotate(dir, 2));
            state = $"cable_node_{open}_{(right ? "r" : "nr")}_{(back ? "b" : "nb")}_{(left ? "l" : "nl")}";
        }
        else
        {
            var name = ent.Comp.Kind.ToString().ToLowerInvariant();
            state = $"{name}_{on}_{open}";
        }

        _appearance.SetData(ent, ImperialModularShieldVisuals.State, state);
    }

    /// <summary>Поворот направления на steps × 45° (положительное — против часовой).</summary>
    private static Direction Rotate(Direction dir, int steps)
    {
        return (Direction) (((int) dir + steps % 8 + 8) % 8);
    }
}
