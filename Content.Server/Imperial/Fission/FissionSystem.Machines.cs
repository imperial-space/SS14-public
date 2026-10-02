using Content.Server.Radiation.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Atmos.Components;
using Content.Shared.Construction;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Burial.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.Fission;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Radiation.Components;
using Content.Shared.Trigger;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Fission;

public sealed partial class FissionSystem
{
    [Dependency] private readonly Content.Server.Atmos.EntitySystems.FlammableSystem _flammable = default!;

    private static readonly TimeSpan MachineInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextMachineUpdate;
    private TimeSpan _nextFalloutTick;

    private readonly List<(EntityUid? Grid, MapId Map, TimeSpan End)> _fallouts = new();

    private void InitializeMachines()
    {
        SubscribeLocalEvent<FissionRodComponent, MapInitEvent>(OnRodMapInit);
        SubscribeLocalEvent<FissionRodComponent, ExaminedEvent>(OnRodExamined);

        SubscribeLocalEvent<FissionGasNodeComponent, AtmosDeviceUpdateEvent>(OnNodeUpdate);
        SubscribeLocalEvent<FissionGasNodeComponent, AnchorStateChangedEvent>(OnNodeAnchorChanged);
        SubscribeLocalEvent<FissionGasNodeComponent, ExaminedEvent>(OnNodeExamined);
        SubscribeLocalEvent<FissionGasNodeComponent, InteractUsingEvent>(OnNodeInteractUsing);
        SubscribeLocalEvent<FissionGasNodeComponent, FissionDoAfterEvent>(OnNodeDoAfter);

        SubscribeLocalEvent<FissionMonitorComponent, BoundUIOpenedEvent>(OnMonitorOpened);
        SubscribeLocalEvent<FissionMonitorComponent, FissionSetThrottleMessage>(OnMonitorThrottle);
        SubscribeLocalEvent<FissionMonitorComponent, FissionToggleVentMessage>(OnMonitorVent);

        SubscribeLocalEvent<FissionCentrifugeComponent, MapInitEvent>(OnCentrifugeMapInit);
        SubscribeLocalEvent<FissionCentrifugeComponent, InteractUsingEvent>(OnCentrifugeInteractUsing);
        SubscribeLocalEvent<FissionCentrifugeComponent, FissionCentrifugeChooseMessage>(OnCentrifugeChoose);
        SubscribeLocalEvent<FissionCentrifugeComponent, GetVerbsEvent<AlternativeVerb>>(OnCentrifugeAltVerbs);
        SubscribeLocalEvent<FissionCentrifugeComponent, AttemptChangePanelEvent>(OnCentrifugePanelAttempt);
        SubscribeLocalEvent<FissionCentrifugeComponent, PanelChangedEvent>((uid, comp, _) => UpdateCentrifugeVisuals((uid, comp)));
        SubscribeLocalEvent<FissionCentrifugeComponent, ExaminedEvent>(OnCentrifugeExamined);

        InitializeFabricator();

        SubscribeLocalEvent<FissionWasteComponent, StartCollideEvent>(OnWasteCollide);
        SubscribeLocalEvent<FissionWasteComponent, InteractUsingEvent>(OnWasteInteractUsing);
        SubscribeLocalEvent<FissionWasteComponent, FissionDoAfterEvent>(OnWasteDoAfter);

        SubscribeLocalEvent<FissionStarterGrenadeComponent, TriggerEvent>(OnStarterTrigger);

        SubscribeLocalEvent<FissionEjectedRodComponent, PreventCollideEvent>(OnEjectedPreventCollide);
        SubscribeLocalEvent<FissionEjectedRodComponent, TimedDespawnEvent>(OnEjectedDespawn);
    }

    private void UpdateMachines(float frameTime, TimeSpan now)
    {
        UpdateCentrifuges(now);
        UpdateFabricators(now);

        if (now >= _nextFalloutTick)
        {
            _nextFalloutTick = now + TimeSpan.FromSeconds(2);
            UpdateFallout(now);
            UpdatePools();
        }

        if (now < _nextMachineUpdate)
            return;

        _nextMachineUpdate = now + MachineInterval;
        UpdateTerminals();
        UpdateMonitors();
        UpdateRodRadiation();
    }

    #region Стержни

    private void OnRodMapInit(Entity<FissionRodComponent> ent, ref MapInitEvent args)
    {
        var rod = ent.Comp;
        if (rod.RandomStats)
        {
            rod.MaxDurability = _random.Next(1000, 10001);
            rod.PowerAmpMod = _random.Next(1, 41) / 10f;
            rod.HeatAmpMod = _random.Next(5, 81) / 10f;
            rod.PowerAmount = _random.Next(10000, 200001);
            rod.HeatAmount = _random.Next(10, 501);
        }

        if (rod.Durability < 0)
            rod.Durability = rod.MaxDurability;

        rod.CurrentHeatMod = rod.HeatAmpMod;
        rod.CurrentPowerMod = rod.PowerAmpMod;
    }

    private void OnRodExamined(Entity<FissionRodComponent> ent, ref ExaminedEvent args)
    {
        var rod = ent.Comp;
        if (rod.Requirements.Count == 0)
        {
            args.PushMarkup(Loc.GetString("fission-rod-no-requirements"));
        }
        else
        {
            var names = rod.Requirements.Select(RequirementName);
            args.PushMarkup(Loc.GetString("fission-rod-requirements", ("list", string.Join(", ", names))));
        }

        if (!args.IsInDetailsRange)
            return;

        var integrity = rod.Infinite ? 100 : Math.Max(rod.Durability, 0) / rod.MaxDurability * 100;
        args.PushMarkup(Loc.GetString("fission-rod-integrity", ("integrity", integrity.ToString("0.#"))));
    }

    public string RequirementName(string requirement)
    {
        if (Enum.TryParse<FissionRodCategory>(requirement, out var category))
            return Loc.GetString($"fission-rod-category-{category}");

        return _proto.TryIndex<EntityPrototype>(requirement, out var proto) ? proto.Name : requirement;
    }

    /// <summary>check_rad_shield: вне камеры и бассейна топливо излучает, в поднятой камере — тоже.</summary>
    private void UpdateRodRadiation()
    {
        var query = EntityQueryEnumerator<FissionRodComponent, RadiationSourceComponent>();
        while (query.MoveNext(out var uid, out var rod, out var source))
        {
            var enabled = rod.RadiationIntensity > 0;
            var intensity = rod.RadiationIntensity;

            if (_container.TryGetContainingContainer((uid, null, null), out var container))
            {
                if (TryComp<FissionChamberComponent>(container.Owner, out var chamber))
                {
                    enabled &= rod.Category == FissionRodCategory.Fuel &&
                               chamber.State is FissionChamberState.Up or FissionChamberState.Open;
                    if (chamber.LinkedReactor is { } reactor && TryComp<FissionReactorComponent>(reactor, out var reactorComp) && !reactorComp.Offline)
                        intensity *= reactorComp.Reactivity;
                }
                else if (HasComp<FissionCentrifugeComponent>(container.Owner))
                {
                    enabled = false;
                }
            }
            else if (OnPool(uid))
            {
                enabled = false;
            }

            if (source.Enabled != enabled)
                _radiation.SetSourceEnabled((uid, source), enabled);
            if (enabled && Math.Abs(source.Intensity - intensity) > 0.01f)
                _radiation.SetIntensity((uid, source), intensity);
        }
    }

    private bool OnPool(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (HasComp<FissionPoolComponent>(anchored))
                return true;
        }

        return false;
    }

    #endregion

    #region Газовые узлы

    private void OnNodeUpdate(Entity<FissionGasNodeComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var node = ent.Comp;
        if (node.LinkedReactor is not { } reactorUid || !TryComp<FissionReactorComponent>(reactorUid, out var reactor))
        {
            node.LinkedReactor = FindNodeReactor(ent);
            return;
        }

        if (reactor.AdminIntervention || reactor.SafetyOverride)
            return;

        if (!_nodeContainer.TryGetNode(ent.Owner, node.NodeName, out PipeNode? pipe))
            return;

        var reactorGas = node.Moderator ? reactor.ModeratorGas : reactor.Air;
        GasMixture network1, network2;
        if (node.Intake)
        {
            network1 = reactorGas;
            network2 = pipe.Air;
        }
        else
        {
            network1 = pipe.Air;
            network2 = reactorGas;
        }

        // Код пассивного клапана.
        var outputPressure = network1.Pressure;
        var inputPressure = network2.Pressure;
        if (network2.TotalMoles <= 0 || network2.Temperature <= 0)
            return;

        var pressureDelta = MathF.Min(node.TargetPressure - outputPressure, (inputPressure - outputPressure) / 2);
        if (node.Intake)
            pressureDelta = MathF.Max(pressureDelta, FissionGasNodeComponent.MinimumMoles);

        var transfer = pressureDelta * network1.Volume / (network2.Temperature * Atmospherics.R);
        if (transfer <= 0)
            return;

        _atmos.Merge(network1, network2.Remove(transfer));
    }

    /// <summary>form_link: реактор должен быть по другую сторону от трубы.</summary>
    private EntityUid? FindNodeReactor(EntityUid node)
    {
        if (!TryGetTile(node, out var grid, out var tile))
            return null;

        var pipeDir = Transform(node).LocalRotation.GetCardinalDir();
        var reactorTile = tile - pipeDir.ToIntVec();
        return TryGetReactorAt(grid.Value, reactorTile, out var reactor) ? reactor.Owner : null;
    }

    private void OnNodeAnchorChanged(Entity<FissionGasNodeComponent> ent, ref AnchorStateChangedEvent args)
    {
        ent.Comp.LinkedReactor = args.Anchored ? FindNodeReactor(ent) : null;
        if (!args.Anchored)
            return;

        if (ent.Comp.LinkedReactor == null)
        {
            _audio.PlayPvs(ent.Comp.BuzzSound, ent, AudioParams.Default.WithVolume(-4));
            _popup.PopupEntity(Loc.GetString("fission-node-link-fail"), ent);
        }
        else
        {
            _audio.PlayPvs(ent.Comp.PingSound, ent, AudioParams.Default.WithVolume(-4));
            _popup.PopupEntity(Loc.GetString("fission-node-link-success"), ent);
        }
    }

    private void OnNodeExamined(Entity<FissionGasNodeComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("fission-node-examine-help"));
        if (!ent.Comp.Moderator)
            args.PushMarkup(Loc.GetString(ent.Comp.Intake ? "fission-node-examine-intake" : "fission-node-examine-extract"));
        args.PushMarkup(Loc.GetString(ent.Comp.LinkedReactor != null ? "fission-node-examine-linked" : "fission-node-examine-unlinked"));
    }

    private void OnNodeInteractUsing(Entity<FissionGasNodeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Moderator || !_tool.HasQuality(args.Used, PulsingQuality))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("fission-node-flipping"), ent, args.User);
        StartDoAfter(args.User, ent, TimeSpan.FromSeconds(1), FissionDoAfterAction.NodeFlip, args.Used);
    }

    private void OnNodeDoAfter(Entity<FissionGasNodeComponent> ent, ref FissionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Action != FissionDoAfterAction.NodeFlip)
            return;

        args.Handled = true;
        ent.Comp.Intake = !ent.Comp.Intake;
        _metaData.SetEntityName(ent, Loc.GetString(ent.Comp.Intake ? "fission-node-name-intake" : "fission-node-name-extract"));
    }

    #endregion

    #region Силовой терминал

    private void UpdateTerminals()
    {
        var query = EntityQueryEnumerator<FissionPowerTerminalComponent, PowerSupplierComponent>();
        while (query.MoveNext(out var uid, out var terminal, out var supplier))
        {
            if (terminal.Reactor is not { } reactorUid || !TryComp<FissionReactorComponent>(reactorUid, out var reactor))
            {
                terminal.Reactor = FindAdjacentReactor(uid);
                supplier.MaxSupply = 0;
                continue;
            }

            supplier.MaxSupply = reactor.CanCreatePower && !reactor.Broken ? MathF.Max(reactor.FinalPower, 0) : 0;
        }
    }

    private EntityUid? FindAdjacentReactor(EntityUid uid)
    {
        if (!TryGetTile(uid, out var grid, out var tile))
            return null;

        foreach (var offset in CardinalOffsets)
        {
            if (TryGetReactorAt(grid.Value, tile + offset, out var reactor))
                return reactor.Owner;
        }

        return null;
    }

    #endregion

    #region Консоль наблюдения

    private EntityUid? FindMonitorReactor(EntityUid monitor)
    {
        var xform = Transform(monitor);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        var query = EntityQueryEnumerator<FissionReactorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var reactorXform))
        {
            if (reactorXform.MapID != xform.MapID)
                continue;

            var distance = (_xform.GetWorldPosition(reactorXform) - _xform.GetWorldPosition(xform)).LengthSquared();
            if (reactorXform.GridUid == xform.GridUid)
                distance -= 1_000_000;
            if (distance >= bestDistance)
                continue;

            best = uid;
            bestDistance = distance;
        }

        return best;
    }

    private void UpdateMonitors()
    {
        var query = EntityQueryEnumerator<FissionMonitorComponent>();
        while (query.MoveNext(out var uid, out var monitor))
        {
            if (!_ui.IsUiOpen(uid, FissionMonitorUiKey.Key))
                continue;

            UpdateMonitorUi((uid, monitor));
        }
    }

    private void OnMonitorOpened(Entity<FissionMonitorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateMonitorUi(ent);
    }

    private void UpdateMonitorUi(Entity<FissionMonitorComponent> ent)
    {
        var monitor = ent.Comp;
        if (monitor.Reactor is not { } existing || !Exists(existing))
            monitor.Reactor = FindMonitorReactor(ent);

        var state = new FissionMonitorUiState();
        if (monitor.Reactor is { } reactorUid && TryComp<FissionReactorComponent>(reactorUid, out var reactor) && !reactor.Broken)
        {
            monitor.Controller = Transform(reactorUid).MapID == Transform(ent).MapID &&
                                 _examine.InRangeUnOccluded(ent.Owner, reactorUid, monitor.ControlRange);

            state.HasReactor = true;
            state.Controlling = monitor.Controller;
            state.Venting = reactor.Venting;
            state.Integrity = GetIntegrity(reactor);
            state.PowerKilowatts = MathF.Round(reactor.FinalPower / 1000);
            state.Temperature = reactor.Air.Temperature;
            state.Pressure = reactor.Air.Pressure;
            state.Coefficient = reactor.Reactivity;
            state.Throttle = reactor.ControlLockout ? 0 : 100 - reactor.DesiredPower;
            state.OperatingPower = 100 - reactor.OperatingPower;
            state.Gases = BuildGasList(reactor.Air, CoolantDescription);
            state.ModeratorGases = BuildGasList(reactor.ModeratorGas, ModeratorDescription);
        }

        _ui.SetUiState(ent.Owner, FissionMonitorUiKey.Key, state);
    }

    private static List<FissionGasEntry> BuildGasList(GasMixture mixture, Func<Gas, string?> describe)
    {
        var list = new List<FissionGasEntry>();
        var total = mixture.TotalMoles;
        foreach (var gas in Enum.GetValues<Gas>())
        {
            var moles = mixture.GetMoles(gas);
            if (moles < 0.01f)
                continue;

            list.Add(new FissionGasEntry(gas.ToString(), moles, MathF.Round(100 * moles / total * 100) / 100, describe(gas)));
        }

        return list;
    }

    private bool MonitorCanControl(Entity<FissionMonitorComponent> ent, out FissionReactorComponent reactor)
    {
        reactor = default!;
        if (!_power.IsPowered(ent.Owner) || ent.Comp.Reactor is not { } uid || !TryComp(uid, out FissionReactorComponent? comp) || comp.Broken)
            return false;

        reactor = comp;
        if (ent.Comp.Controller)
            return true;

        _popup.PopupEntity(Loc.GetString("fission-monitor-out-of-sight"), ent, PopupType.MediumCaution);
        _audio.PlayPvs(ent.Comp.BuzzSound, ent);
        return false;
    }

    private void OnMonitorThrottle(Entity<FissionMonitorComponent> ent, ref FissionSetThrottleMessage args)
    {
        if (!MonitorCanControl(ent, out var reactor) || reactor.ControlLockout)
            return;

        reactor.DesiredPower = 100 - Math.Clamp(MathF.Round(args.Throttle), 0, 100);
        UpdateMonitorUi(ent);
    }

    private void OnMonitorVent(Entity<FissionMonitorComponent> ent, ref FissionToggleVentMessage args)
    {
        if (!MonitorCanControl(ent, out var reactor))
            return;

        if (reactor.VentLockout)
        {
            _audio.PlayPvs(ent.Comp.BuzzSound, ent);
            _popup.PopupEntity(Loc.GetString("fission-monitor-vent-stuck"), ent, PopupType.MediumCaution);
            return;
        }

        reactor.Venting = !reactor.Venting;
        UpdateMonitorUi(ent);
    }

    #endregion

    #region Центрифуга

    private void OnCentrifugeMapInit(Entity<FissionCentrifugeComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, FissionCentrifugeComponent.ContainerId);
        UpdateCentrifugeVisuals(ent);
    }

    private EntityUid? CentrifugeRod(EntityUid uid)
    {
        return _container.TryGetContainer(uid, FissionCentrifugeComponent.ContainerId, out var container) &&
               container.ContainedEntities.Count > 0
            ? container.ContainedEntities[0]
            : null;
    }

    private List<string> EnrichmentOptions(FissionRodComponent rod)
    {
        var options = new List<string>();
        if (rod.PowerEnrichProgress >= rod.EnrichmentCycles && rod.PowerEnrichResult is { } power)
            options.Add(power);
        if (rod.HeatEnrichProgress >= rod.EnrichmentCycles && rod.HeatEnrichResult is { } heat)
            options.Add(heat);
        return options;
    }

    private void OnCentrifugeInteractUsing(Entity<FissionCentrifugeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<FissionRodComponent>(args.Used, out var rod) || rod.Category != FissionRodCategory.Fuel)
            return;

        args.Handled = true;
        if (!_power.IsPowered(ent.Owner))
            return;

        if (TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open)
        {
            _popup.PopupEntity(Loc.GetString("fission-centrifuge-panel-open"), ent, args.User);
            return;
        }

        if (ent.Comp.Active)
        {
            _popup.PopupEntity(Loc.GetString("fission-centrifuge-busy"), ent, args.User);
            return;
        }

        if (CentrifugeRod(ent) != null)
        {
            _popup.PopupEntity(Loc.GetString("fission-centrifuge-occupied"), ent, args.User);
            return;
        }

        var options = EnrichmentOptions(rod);
        if (options.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("fission-centrifuge-no-enrichment"), ent, args.User);
            return;
        }

        if (options.Count == 1)
        {
            BeginEnrichment(ent, args.Used, options[0]);
            return;
        }

        // show_radial_menu: выбор продукта обогащения.
        ent.Comp.PendingRod = args.Used;
        _ui.OpenUi(ent.Owner, FissionCentrifugeUiKey.Key, args.User);
        _ui.SetUiState(ent.Owner, FissionCentrifugeUiKey.Key, new FissionCentrifugeUiState(options));
    }

    private void OnCentrifugeChoose(Entity<FissionCentrifugeComponent> ent, ref FissionCentrifugeChooseMessage args)
    {
        _ui.CloseUi(ent.Owner, FissionCentrifugeUiKey.Key, args.Actor);
        if (ent.Comp.PendingRod is not { } rodUid || ent.Comp.Active || CentrifugeRod(ent) != null)
            return;

        ent.Comp.PendingRod = null;
        if (!TryComp<FissionRodComponent>(rodUid, out var rod) || !EnrichmentOptions(rod).Contains(args.Result))
            return;
        if (!_hands.IsHolding(args.Actor, rodUid) || !_power.IsPowered(ent.Owner))
            return;

        BeginEnrichment(ent, rodUid, args.Result);
    }

    private void BeginEnrichment(Entity<FissionCentrifugeComponent> ent, EntityUid rod, string result)
    {
        var slot = _container.EnsureContainer<ContainerSlot>(ent, FissionCentrifugeComponent.ContainerId);
        if (!_container.Insert(rod, slot))
            return;

        ent.Comp.Result = result;
        ent.Comp.Active = true;
        ent.Comp.EndTime = _timing.CurTime + ent.Comp.WorkTime;
        SetCentrifugeLoad(ent, true);
        _audio.PlayPvs(ent.Comp.StartSound, ent);
        _ambient.SetAmbience(ent, true);
        UpdateCentrifugeVisuals(ent);
    }

    private void SetCentrifugeLoad(Entity<FissionCentrifugeComponent> ent, bool active)
    {
        if (TryComp<ApcPowerReceiverComponent>(ent, out var receiver))
            receiver.Load = active ? ent.Comp.ActiveLoad : ent.Comp.IdleLoad;
    }

    private void UpdateCentrifuges(TimeSpan now)
    {
        var query = EntityQueryEnumerator<FissionCentrifugeComponent>();
        while (query.MoveNext(out var uid, out var centrifuge))
        {
            if (!centrifuge.Active)
                continue;

            var ent = (uid, centrifuge);
            if (!_power.IsPowered(uid))
            {
                centrifuge.Active = false;
                SetCentrifugeLoad(ent, false);
                _audio.PlayPvs(centrifuge.BuzzSound, uid);
                _ambient.SetAmbience(uid, false);
                UpdateCentrifugeVisuals(ent);
                continue;
            }

            if (now < centrifuge.EndTime)
                continue;

            centrifuge.Active = false;
            SetCentrifugeLoad(ent, false);
            _audio.PlayPvs(centrifuge.PingSound, uid);
            _ambient.SetAmbience(uid, false);
            if (CentrifugeRod(uid) is { } old && centrifuge.Result is { } result)
            {
                Del(old);
                var spawned = Spawn(result, Transform(uid).Coordinates);
                _container.Insert(spawned, _container.GetContainer(uid, FissionCentrifugeComponent.ContainerId));
            }

            centrifuge.Result = null;
            UpdateCentrifugeVisuals(ent);
        }
    }

    private void OnCentrifugeAltVerbs(Entity<FissionCentrifugeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || CentrifugeRod(ent) is not { } rod)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fission-centrifuge-verb-eject"),
            Act = () =>
            {
                if (ent.Comp.Active)
                {
                    _popup.PopupEntity(Loc.GetString("fission-centrifuge-eject-running"), ent, user);
                    return;
                }

                _container.TryRemoveFromContainer(rod, true);
                _hands.TryPickupAnyHand(user, rod);
                _audio.PlayPvs(ent.Comp.EjectSound, ent);
                UpdateCentrifugeVisuals(ent);
            },
        });
    }

    private void OnCentrifugePanelAttempt(Entity<FissionCentrifugeComponent> ent, ref AttemptChangePanelEvent args)
    {
        if (args.Cancelled || CentrifugeRod(ent) == null)
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("fission-centrifuge-panel-rod"), ent, user);
    }

    private void OnCentrifugeExamined(Entity<FissionCentrifugeComponent> ent, ref ExaminedEvent args)
    {
        if (CentrifugeRod(ent) != null)
            args.PushMarkup(Loc.GetString("fission-centrifuge-examine-eject"));
    }

    private void UpdateCentrifugeVisuals(Entity<FissionCentrifugeComponent> ent)
    {
        string state;
        if (CentrifugeRod(ent) == null)
            state = "centrifuge_empty";
        else if (TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open)
            state = "centrifuge_maint";
        else
            state = ent.Comp.Active ? "centrifuge_on" : "centrifuge_full";

        _appearance.SetData(ent, FissionMachineVisuals.State, state);
    }

    #endregion

    #region Отходы, активатор, бассейн, выброшенный стержень

    private void OnWasteCollide(Entity<FissionWasteComponent> ent, ref StartCollideEvent args)
    {
        if (!HasComp<MobStateComponent>(args.OtherEntity))
            return;

        _audio.PlayPvs(ent.Comp.StepSound, ent);
        if (_random.Prob(0.5f))
            _radiation.IrradiateEntity(args.OtherEntity, 5, 1, ent);
    }

    private void OnWasteInteractUsing(Entity<FissionWasteComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<ShovelComponent>(args.Used))
            return;

        args.Handled = true;
        SpawnRadiationPulse(ent, 5);
        _popup.PopupEntity(Loc.GetString("fission-waste-clearing"), ent, args.User);
        StartDoAfter(args.User, ent, ent.Comp.ClearTime, FissionDoAfterAction.WasteClear, args.Used);
    }

    private void OnWasteDoAfter(Entity<FissionWasteComponent> ent, ref FissionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Action != FissionDoAfterAction.WasteClear)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("fission-waste-cleared"), ent, args.User);
        QueueDel(ent);
    }

    /// <summary>nuclear_starter/prime: принудительно запускает опущенные камеры со стержнями.</summary>
    private void OnStarterTrigger(Entity<FissionStarterGrenadeComponent> ent, ref TriggerEvent args)
    {
        var chambers = new HashSet<Entity<FissionChamberComponent>>();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.Range, chambers);

        foreach (var chamber in chambers)
        {
            if (chamber.Comp.State == FissionChamberState.Down && GetRod(chamber) != null)
                chamber.Comp.Operational = true;
        }

        foreach (var chamber in chambers)
        {
            chamber.Comp.RequirementsMet = CheckStatus(chamber);
            UpdateChamberVisuals(chamber);
        }
    }

    /// <summary>poolcontroller/nuclear: бассейн тушит и отмывает тех, кто в нём.</summary>
    private void UpdatePools()
    {
        var query = EntityQueryEnumerator<FissionPoolComponent, TransformComponent>();
        var nearby = new HashSet<Entity<FlammableComponent>>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            nearby.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, 0.45f, nearby);
            foreach (var flammable in nearby)
            {
                if (flammable.Comp.OnFire || flammable.Comp.FireStacks > 0)
                    _flammable.Extinguish(flammable, flammable.Comp);
            }
        }
    }

    private void OnEjectedPreventCollide(Entity<FissionEjectedRodComponent> ent, ref PreventCollideEvent args)
    {
        if (HasComp<FissionReactorComponent>(args.OtherEntity) || HasComp<FissionChamberComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnEjectedDespawn(Entity<FissionEjectedRodComponent> ent, ref TimedDespawnEvent args)
    {
        if (_container.TryGetContainer(ent, FissionEjectedRodComponent.ContainerId, out var container))
            _container.EmptyContainer(container, true, _xform.GetMoverCoordinates(ent));
    }

    #endregion

    #region Радиоактивные осадки

    private void StartFallout(EntityUid? grid, MapId map, TimeSpan duration)
    {
        _fallouts.Add((grid, map, _timing.CurTime + TimeSpan.FromSeconds(5) + duration));
    }

    /// <summary>rad_storm/nuclear_fallout: облучение всех живых на станции, пока идут осадки.</summary>
    private void UpdateFallout(TimeSpan now)
    {
        if (_fallouts.Count == 0)
            return;

        _fallouts.RemoveAll(f => now >= f.End);
        if (_fallouts.Count == 0)
            return;

        var query = EntityQueryEnumerator<RadiationReceiverComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            foreach (var (grid, map, _) in _fallouts)
            {
                if (xform.MapID != map || grid != null && xform.GridUid != grid)
                    continue;

                _radiation.IrradiateEntity(uid, 0.3f, 2);
                break;
            }
        }
    }

    #endregion
}
