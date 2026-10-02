using Content.Shared.Damage.Components;
using Robust.Shared.Random;
using Content.Shared.Construction;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.Fission;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Imperial.Fission;

public sealed partial class FissionSystem
{
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private const string ChamberFixture = "fix1";
    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype> HeatDamageType = "Heat";
    private static readonly TimeSpan ChamberHeatInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextChamberHeat;

    private void InitializeChambers()
    {
        SubscribeLocalEvent<FissionChamberComponent, MapInitEvent>(OnChamberMapInit);
        SubscribeLocalEvent<FissionChamberComponent, ActivateInWorldEvent>(OnChamberActivate);
        SubscribeLocalEvent<FissionChamberComponent, GetVerbsEvent<AlternativeVerb>>(OnChamberAltVerbs);
        SubscribeLocalEvent<FissionChamberComponent, InteractUsingEvent>(OnChamberInteractUsing);
        SubscribeLocalEvent<FissionChamberComponent, FissionDoAfterEvent>(OnChamberDoAfter);
        SubscribeLocalEvent<FissionChamberComponent, ExaminedEvent>(OnChamberExamined);
        SubscribeLocalEvent<FissionChamberComponent, AttemptChangePanelEvent>(OnChamberPanelAttempt);
        SubscribeLocalEvent<FissionChamberComponent, PanelChangedEvent>(OnChamberPanelChanged);
        SubscribeLocalEvent<FissionChamberComponent, MachineDeconstructedEvent>(OnChamberDeconstructed);
        SubscribeLocalEvent<FissionChamberComponent, EntityTerminatingEvent>(OnChamberTerminating);
    }

    private void OnChamberMapInit(Entity<FissionChamberComponent> ent, ref MapInitEvent args)
    {
        var slot = _container.EnsureContainer<ContainerSlot>(ent, FissionChamberComponent.ContainerId);
        if (ent.Comp.StartingRod is { } rod && slot.ContainedEntity == null)
        {
            var spawned = Spawn(rod, Transform(ent).Coordinates);
            _container.Insert(spawned, slot);
        }

        SetChamberDensity(ent, false);
        UpdateChamberVisuals(ent);
    }

    public Entity<FissionRodComponent>? GetRod(EntityUid chamber)
    {
        if (!_container.TryGetContainer(chamber, FissionChamberComponent.ContainerId, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            return null;
        }

        var rod = container.ContainedEntities[0];
        return TryComp<FissionRodComponent>(rod, out var comp) ? (rod, comp) : null;
    }

    #region Взаимодействие

    private bool AdminFrozen(FissionChamberComponent chamber, EntityUid user)
    {
        if (chamber.LinkedReactor is not { } reactor || !TryComp<FissionReactorComponent>(reactor, out var comp) || !comp.AdminIntervention)
            return false;

        _popup.PopupEntity(Loc.GetString("fission-chamber-frozen"), user, user);
        return true;
    }

    private bool ReactorOnline(FissionChamberComponent chamber)
    {
        return chamber.LinkedReactor is { } reactor && TryComp<FissionReactorComponent>(reactor, out var comp) && !comp.Offline;
    }

    /// <summary>attack_hand: поднять, опустить камеру или вынуть стержень.</summary>
    private void OnChamberActivate(Entity<FissionChamberComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        var user = args.User;
        var chamber = ent.Comp;
        if (AdminFrozen(chamber, user))
            return;

        if (!_power.IsPowered(ent))
        {
            _popup.PopupEntity(Loc.GetString("fission-chamber-no-power"), ent, user);
            return;
        }

        switch (chamber.State)
        {
            case FissionChamberState.Down:
            case FissionChamberState.OverloadIdle:
            {
                if (chamber.Welded)
                {
                    _popup.PopupEntity(Loc.GetString("fission-chamber-welded"), ent, user);
                    return;
                }

                if (!DensityCheck(ent, user))
                    return;

                var delay = TimeSpan.FromSeconds(1);
                if (ReactorOnline(chamber))
                {
                    delay = TimeSpan.FromSeconds(8);
                    BurnHandler(ent, user);
                }

                StartDoAfter(user, ent, delay, FissionDoAfterAction.ChamberRaise);
                return;
            }
            case FissionChamberState.Up:
                if (DensityCheck(ent, user))
                    StartDoAfter(user, ent, TimeSpan.FromSeconds(2), FissionDoAfterAction.ChamberLower);
                return;
            case FissionChamberState.Open:
            {
                if (GetRod(ent) is not { } rod)
                {
                    _popup.PopupEntity(Loc.GetString("fission-chamber-no-rod"), ent, user);
                    return;
                }

                if (!_hands.TryPickupAnyHand(user, rod))
                {
                    _popup.PopupEntity(Loc.GetString("fission-chamber-hands-full"), ent, user);
                    return;
                }

                _audio.PlayPvs(chamber.RemoveSound, ent);
                UpdateChamberVisuals(ent);
                return;
            }
            case FissionChamberState.OverloadActive:
                _popup.PopupEntity(Loc.GetString("fission-chamber-lockdown"), ent, user, PopupType.MediumCaution);
                return;
        }
    }

    /// <summary>attack_hand_secondary: открыть или закрыть защитные створки поднятой камеры.</summary>
    private void OnChamberAltVerbs(Entity<FissionChamberComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        var chamber = ent.Comp;
        var user = args.User;
        if (chamber.State == FissionChamberState.Up)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("fission-chamber-verb-open"),
                Act = () => TryToggleShield(ent, user),
            });
        }
        else if (chamber.State == FissionChamberState.Open)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("fission-chamber-verb-close"),
                Act = () => TryToggleShield(ent, user),
            });
        }
    }

    private void TryToggleShield(Entity<FissionChamberComponent> ent, EntityUid user)
    {
        var chamber = ent.Comp;
        if (_timing.CurTime < chamber.LockoutUntil || AdminFrozen(chamber, user))
            return;

        if (chamber.State == FissionChamberState.Up)
        {
            OpenChamber(ent);
            return;
        }

        if (chamber.State != FissionChamberState.Open)
            return;

        if (chamber.PanelOpen)
        {
            _popup.PopupEntity(Loc.GetString("fission-chamber-panel-open"), ent, user);
            return;
        }

        CloseChamber(ent);
    }

    private void OnChamberInteractUsing(Entity<FissionChamberComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var chamber = ent.Comp;
        var user = args.User;

        if (HasComp<FissionRodComponent>(args.Used))
        {
            if (chamber.State != FissionChamberState.Open)
                return;

            args.Handled = true;
            if (GetRod(ent) != null)
            {
                _popup.PopupEntity(Loc.GetString("fission-chamber-occupied"), ent, user);
                return;
            }

            if (chamber.PanelOpen)
            {
                _popup.PopupEntity(Loc.GetString("fission-chamber-panel-blocks"), ent, user);
                return;
            }

            var slot = _container.EnsureContainer<ContainerSlot>(ent, FissionChamberComponent.ContainerId);
            if (!_container.Insert(args.Used, slot))
                return;

            _audio.PlayPvs(chamber.InsertSound, ent);
            UpdateChamberVisuals(ent);
            return;
        }

        if (_tool.HasQuality(args.Used, WeldingQuality))
        {
            args.Handled = true;
            if (_combat.IsInCombatMode(user))
            {
                if (chamber.State is FissionChamberState.OverloadIdle or FissionChamberState.OverloadActive)
                {
                    _popup.PopupEntity(Loc.GetString("fission-chamber-weld-overload"), ent, user);
                    return;
                }

                if (chamber.State != FissionChamberState.Down)
                    return;

                _popup.PopupEntity(Loc.GetString(chamber.Welded ? "fission-chamber-unwelding" : "fission-chamber-welding"), ent, user);
                _tool.UseTool(args.Used, user, ent, 6, WeldingQuality, new FissionDoAfterEvent(FissionDoAfterAction.ChamberWeld), fuel: 1);
                return;
            }

            if (TryComp<DamageableComponent>(ent, out var damageable) && _damageable.GetAllDamage((ent.Owner, damageable)).GetTotal() > 0)
            {
                _popup.PopupEntity(Loc.GetString("fission-chamber-repairing"), ent, user);
                _tool.UseTool(args.Used, user, ent, 3, WeldingQuality, new FissionDoAfterEvent(FissionDoAfterAction.ChamberRepair), fuel: 1);
                return;
            }

            _popup.PopupEntity(Loc.GetString("fission-chamber-no-repair"), ent, user);
        }
    }

    private void StartDoAfter(EntityUid user, EntityUid target, TimeSpan delay, FissionDoAfterAction action, EntityUid? used = null)
    {
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, new FissionDoAfterEvent(action), target, target, used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnChamberDoAfter(Entity<FissionChamberComponent> ent, ref FissionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var chamber = ent.Comp;
        switch (args.Action)
        {
            case FissionDoAfterAction.ChamberRaise:
                if (chamber.State is FissionChamberState.Down or FissionChamberState.OverloadIdle && !chamber.Welded &&
                    DensityCheck(ent, args.User))
                {
                    RaiseChamber(ent);
                }
                break;
            case FissionDoAfterAction.ChamberLower:
                if (chamber.State == FissionChamberState.Up && DensityCheck(ent, args.User))
                    LowerChamber(ent);
                break;
            case FissionDoAfterAction.ChamberWeld:
                if (chamber.State != FissionChamberState.Down)
                    break;
                if (chamber.Welded)
                    Unweld(ent);
                else
                    WeldShut(ent);
                break;
            case FissionDoAfterAction.ChamberRepair:
                _damageable.SetAllDamage(ent.Owner, 0);
                break;
        }
    }

    /// <summary>default_deconstruction_screwdriver: панель открывается только у открытой пустой камеры при выключенном реакторе.</summary>
    private void OnChamberPanelAttempt(Entity<FissionChamberComponent> ent, ref AttemptChangePanelEvent args)
    {
        if (args.Cancelled)
            return;

        string? reason = null;
        if (ent.Comp.State != FissionChamberState.Open)
            reason = "fission-chamber-panel-must-open";
        else if (ReactorOnline(ent.Comp))
            reason = "fission-chamber-panel-reactor-on";
        else if (GetRod(ent) != null)
            reason = "fission-chamber-panel-rod";

        if (reason == null)
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString(reason), ent, user);
    }

    private void OnChamberPanelChanged(Entity<FissionChamberComponent> ent, ref PanelChangedEvent args)
    {
        ent.Comp.PanelOpen = args.Open;
        UpdateChamberVisuals(ent);
    }

    private void OnChamberDeconstructed(Entity<FissionChamberComponent> ent, ref MachineDeconstructedEvent args)
    {
        DropRod(ent);
    }

    private void OnChamberTerminating(Entity<FissionChamberComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.LinkedReactor is { } reactor && TryComp<FissionReactorComponent>(reactor, out var comp))
            comp.ConnectedChambers.Remove(ent);
    }

    private void DropRod(EntityUid chamber)
    {
        if (_container.TryGetContainer(chamber, FissionChamberComponent.ContainerId, out var container))
            _container.EmptyContainer(container, true, Transform(chamber).Coordinates);
    }

    private void OnChamberExamined(Entity<FissionChamberComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(FissionChamberComponent)))
        {
            args.PushMarkup(Loc.GetString("fission-chamber-examine-help"));
            foreach (var line in GetDeepExamineInfo(ent))
                args.PushMarkup(line);
        }
    }

    /// <summary>get_deep_examine_info: сведения о стержне, мощности и обогащении.</summary>
    private List<string> GetDeepExamineInfo(Entity<FissionChamberComponent> ent)
    {
        var lines = new List<string>();
        var chamber = ent.Comp;
        if (chamber.State != FissionChamberState.Down)
            return lines;

        if (GetRod(ent) is not { } rod)
        {
            lines.Add(Loc.GetString("fission-chamber-info-no-rod"));
            return lines;
        }

        if (chamber.LinkedReactor is not { } reactorUid || !TryComp<FissionReactorComponent>(reactorUid, out var reactor))
        {
            lines.Add(Loc.GetString("fission-chamber-info-no-reactor"));
            return lines;
        }

        var operatingRate = OperatingRate(reactor);
        var durabilityMod = DurabilityMod(rod.Comp);
        lines.Add(Loc.GetString("fission-chamber-info-rod", ("rod", rod.Owner)));

        if (!rod.Comp.Infinite && rod.Comp.Durability <= 0)
        {
            lines.Add(Loc.GetString("fission-chamber-info-depleted"));
            return lines;
        }

        var integrity = rod.Comp.Infinite ? 100 : rod.Comp.Durability / rod.Comp.MaxDurability * 100;
        lines.Add(Loc.GetString("fission-chamber-info-integrity", ("integrity", integrity.ToString("0.##"))));

        if (chamber.PowerTotal != 0 && chamber.Operational)
        {
            lines.Add(Loc.GetString("fission-chamber-info-power",
                ("power", (chamber.PowerTotal * operatingRate * durabilityMod / 1000).ToString("0.##")),
                ("mod", chamber.PowerModTotal.ToString("0.##"))));
        }
        else
        {
            lines.Add(Loc.GetString("fission-chamber-info-no-power"));
        }

        if (rod.Comp.Category == FissionRodCategory.Fuel && rod.Comp.PowerEnrichResult != null)
        {
            lines.Add(Loc.GetString(rod.Comp.PowerEnrichProgress >= rod.Comp.EnrichmentCycles
                ? "fission-chamber-info-power-enriched"
                : "fission-chamber-info-power-not-enriched"));
        }

        if (chamber.HeatTotal != 0)
        {
            lines.Add(Loc.GetString("fission-chamber-info-heat",
                ("heat", (chamber.HeatTotal * FissionReactorComponent.HeatModifier * operatingRate * durabilityMod).ToString("0.##")),
                ("mod", chamber.HeatModTotal.ToString("0.##"))));
        }
        else
        {
            lines.Add(Loc.GetString("fission-chamber-info-no-heat"));
        }

        if (rod.Comp.Category == FissionRodCategory.Fuel && rod.Comp.HeatEnrichResult != null)
        {
            lines.Add(Loc.GetString(rod.Comp.HeatEnrichProgress >= rod.Comp.EnrichmentCycles
                ? "fission-chamber-info-heat-enriched"
                : "fission-chamber-info-heat-not-enriched"));
        }

        return lines;
    }

    private bool DensityCheck(EntityUid chamber, EntityUid user)
    {
        foreach (var other in _lookup.GetEntitiesInRange(Transform(chamber).Coordinates, 0.2f, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (other == chamber)
                continue;
            if (!TryComp<PhysicsComponent>(other, out var physics) || !physics.CanCollide || !physics.Hard)
                continue;
            if (HasComp<FissionChamberComponent>(other))
                continue;

            _popup.PopupEntity(Loc.GetString("fission-chamber-blocked"), chamber, user);
            return false;
        }

        return true;
    }

    /// <summary>burn_handler: горячая камера обжигает руки.</summary>
    private void BurnHandler(Entity<FissionChamberComponent> ent, EntityUid user)
    {
        var burn = FissionChamberComponent.HeatDamage;
        if (ent.Comp.LinkedReactor is { } reactor && TryComp<FissionReactorComponent>(reactor, out var comp) && CheckOverheating(comp))
            burn *= 2;

        if (_inventory.TryGetSlotEntity(user, "gloves", out _))
            burn *= 0.5f;

        var damage = new DamageSpecifier(_proto.Index(HeatDamageType), burn);
        _damageable.TryChangeDamage(user, damage, true, origin: ent);
    }

    #endregion

    #region Состояния камеры

    private void SetChamberDensity(EntityUid uid, bool dense)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures) || !fixtures.Fixtures.TryGetValue(ChamberFixture, out var fixture))
            return;

        _physics.SetHard(uid, fixture, dense, fixtures);
    }

    private void RaiseChamber(Entity<FissionChamberComponent> ent, bool playSound = true)
    {
        var chamber = ent.Comp;
        chamber.State = FissionChamberState.Up;
        chamber.LockoutUntil = _timing.CurTime + TimeSpan.FromSeconds(0.7);
        SetChamberDensity(ent, true);
        chamber.Operational = false;
        chamber.Enriching = false;
        chamber.RequirementsMet = false;
        chamber.PowerTotal = 0;

        if (playSound)
            _audio.PlayPvs(chamber.MoveSound, ent);

        UpdateChamberVisuals(ent);
        foreach (var neighbor in chamber.Neighbors)
        {
            if (!TryComp<FissionChamberComponent>(neighbor, out var neighborComp) || GetRod(neighbor) == null)
                continue;

            neighborComp.RequirementsMet = CheckStatus((neighbor, neighborComp));
            UpdateChamberVisuals((neighbor, neighborComp));
        }
    }

    private void LowerChamber(Entity<FissionChamberComponent> ent, bool playSound = true)
    {
        var chamber = ent.Comp;
        SetChamberDensity(ent, false);
        if (playSound)
            _audio.PlayPvs(chamber.MoveSound, ent);

        if (chamber.LinkedReactor is { } reactorUid && TryComp<FissionReactorComponent>(reactorUid, out var reactor) && reactor.SafetyOverride)
        {
            chamber.State = FissionChamberState.OverloadIdle;
            UpdateChamberVisuals(ent);
            if (CheckOverloadReady(reactor))
                SetOverload((reactorUid, reactor));
            return;
        }

        chamber.State = FissionChamberState.Down;
        if (GetRod(ent) is not { } rod)
        {
            UpdateChamberVisuals(ent);
            return;
        }

        chamber.DurabilityLevel = DurabilityLevel(rod.Comp);
        chamber.RequirementsMet = CheckStatus(ent);
        UpdateChamberVisuals(ent);
    }

    private void OpenChamber(Entity<FissionChamberComponent> ent, bool playSound = true)
    {
        ent.Comp.State = FissionChamberState.Open;
        ent.Comp.LockoutUntil = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        if (playSound)
            _audio.PlayPvs(ent.Comp.SwitchSound, ent);
        UpdateChamberVisuals(ent);
    }

    private void CloseChamber(Entity<FissionChamberComponent> ent, bool playSound = true)
    {
        ent.Comp.State = FissionChamberState.Up;
        ent.Comp.LockoutUntil = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        if (playSound)
            _audio.PlayPvs(ent.Comp.SwitchSound, ent);
        UpdateChamberVisuals(ent);
    }

    private void SetIdleOverload(Entity<FissionChamberComponent> ent)
    {
        var chamber = ent.Comp;
        if (chamber.State == FissionChamberState.Down)
            chamber.State = FissionChamberState.OverloadIdle;
        chamber.Welded = false;
        chamber.Operational = false;
        chamber.Enriching = false;
        chamber.RequirementsMet = false;
        UpdateChamberVisuals(ent);
    }

    private void SetActiveOverload(Entity<FissionChamberComponent> ent)
    {
        ent.Comp.State = FissionChamberState.OverloadActive;
        UpdateChamberVisuals(ent);
    }

    private void WeldShut(Entity<FissionChamberComponent> ent)
    {
        ent.Comp.Welded = true;
        _audio.PlayPvs(ent.Comp.WeldSound, ent);
        UpdateChamberVisuals(ent);
    }

    private void Unweld(Entity<FissionChamberComponent> ent)
    {
        ent.Comp.Welded = false;
        _audio.PlayPvs(ent.Comp.WeldSound, ent);
        UpdateChamberVisuals(ent);
    }

    /// <summary>eject_rod: авария выстреливает стержень охладителя через станцию.</summary>
    private void EjectRod(Entity<FissionChamberComponent> ent)
    {
        RaiseChamber(ent, false);
        OpenChamber(ent, false);
        SpawnSmoke(ent);
        SpawnRadiationPulse(ent, 4);

        if (GetRod(ent) is not { } rod)
            return;

        var coords = _xform.GetMapCoordinates(ent);
        var projectile = Spawn(ent.Comp.EjectedRod, coords);
        var holder = _container.EnsureContainer<ContainerSlot>(projectile, FissionEjectedRodComponent.ContainerId);
        _container.Insert(rod.Owner, holder, force: true);

        var distance = _random.Next(10, 61);
        var despawn = EnsureComp<Robust.Shared.Spawners.TimedDespawnComponent>(projectile);
        despawn.Lifetime = distance / 15f;

        UpdateChamberVisuals(ent);
        _audio.PlayPvs(ent.Comp.BangSound, ent, AudioParams.Default.WithVolume(4));
        _popup.PopupEntity(Loc.GetString("fission-chamber-pow"), ent, PopupType.LargeCaution);
    }

    #endregion

    #region process() камеры

    private void ProcessChamber(Entity<FissionChamberComponent> ent, FissionReactorComponent reactor)
    {
        var chamber = ent.Comp;
        if (reactor.AdminIntervention || chamber.State != FissionChamberState.Down || GetRod(ent) is not { } rod)
            return;

        var level = DurabilityLevel(rod.Comp);
        if (level != chamber.DurabilityLevel)
        {
            chamber.DurabilityLevel = level;
            UpdateChamberVisuals(ent);
        }

        if (!reactor.Offline)
            CalcStatDecrease(rod.Comp);

        if (chamber.Operational && !rod.Comp.Infinite && rod.Comp.Durability <= 0)
        {
            chamber.RequirementsMet = false;
            UpdateChamberVisuals(ent);
            return;
        }

        if (!chamber.RequirementsMet && !chamber.Operational)
        {
            if (CheckStatus(ent))
            {
                chamber.RequirementsMet = true;
                UpdateChamberVisuals(ent);
            }
            return;
        }

        if (chamber.RequirementsMet && !chamber.Operational)
        {
            if (_random.Prob(0.2f))
            {
                chamber.Operational = true;
                UpdateChamberVisuals(ent);
            }
            return;
        }

        if (!chamber.RequirementsMet && chamber.Operational)
        {
            // Охладители, потеряв условия, отказывают чаще.
            if (rod.Comp.Category == FissionRodCategory.Coolant)
            {
                if (_random.Prob(0.15f))
                {
                    chamber.Operational = false;
                    UpdateChamberVisuals(ent);
                }
            }
            else if (_random.Prob(0.01f))
            {
                chamber.Enriching = false;
                chamber.Operational = false;
                UpdateChamberVisuals(ent);
            }
        }
    }

    /// <summary>process_atmos: поднятая или открытая камера при работающем реакторе греет воздух вокруг.</summary>
    private void UpdateChambers(TimeSpan now)
    {
        if (now < _nextChamberHeat)
            return;

        _nextChamberHeat = now + ChamberHeatInterval;
        var query = EntityQueryEnumerator<FissionChamberComponent>();
        while (query.MoveNext(out var uid, out var chamber))
        {
            if (chamber.State is not (FissionChamberState.Up or FissionChamberState.Open))
                continue;
            if (!ReactorOnline(chamber) || GetRod(uid) == null)
                continue;
            if (chamber.LinkedReactor is { } reactor && Comp<FissionReactorComponent>(reactor).AdminIntervention)
                continue;
            if (_atmos.GetContainingMixture(uid, false, true) is not { } environment)
                continue;

            var heatCapacity = _atmos.GetHeatCapacity(environment, true);
            if (heatCapacity <= 0)
                continue;

            var heatChange = chamber.HeatTotal / heatCapacity * FissionReactorComponent.HeatModifier;
            if (chamber.State == FissionChamberState.Up)
                heatChange *= 0.25f;
            heatChange = MathF.Max(heatChange, 1);
            environment.Temperature += heatChange;
        }
    }

    /// <summary>calculate_stats: базовые значения стержня, умноженные на модификаторы соседей.</summary>
    private void CalculateStats(Entity<FissionChamberComponent> ent, Entity<FissionRodComponent> rod, float operatingRate)
    {
        var chamber = ent.Comp;
        chamber.PowerTotal = rod.Comp.PowerAmount * operatingRate;
        chamber.HeatTotal = rod.Comp.HeatAmount * operatingRate;
        chamber.PowerModTotal = 1;
        chamber.HeatModTotal = 1;

        foreach (var neighbor in chamber.Neighbors)
        {
            if (!TryComp<FissionChamberComponent>(neighbor, out var neighborComp) || neighborComp.State == FissionChamberState.Open)
                continue;
            if (GetRod(neighbor) is not { } neighborRod)
                continue;

            if (neighborRod.Comp.Category == FissionRodCategory.Coolant && !neighborComp.Operational)
            {
                // Выключенный охладитель не снижает нагрев.
            }
            else if (rod.Comp.HeatAmount > 0)
            {
                chamber.HeatModTotal *= neighborRod.Comp.CurrentHeatMod;
            }

            if (chamber.Operational && neighborComp.State == FissionChamberState.Down && rod.Comp.PowerAmount > 0)
                chamber.PowerModTotal *= neighborRod.Comp.CurrentPowerMod;
        }

        chamber.PowerTotal *= chamber.PowerModTotal;
        chamber.HeatTotal *= chamber.HeatModTotal;
    }

    /// <summary>check_status: выполнены ли требования стержня к работающим соседям.</summary>
    private bool CheckStatus(Entity<FissionChamberComponent> ent)
    {
        if (GetRod(ent) is not { } rod)
            return false;

        var requirements = new List<string>(rod.Comp.Requirements);
        if (requirements.Count == 0)
            return true;

        foreach (var neighbor in ent.Comp.Neighbors)
        {
            if (!TryComp<FissionChamberComponent>(neighbor, out var neighborComp) || !neighborComp.Operational)
                continue;
            if (GetRod(neighbor) is not { } neighborRod)
                continue;

            var id = MetaData(neighborRod).EntityPrototype?.ID;
            if (id != null && requirements.Remove(id))
                continue;

            requirements.Remove(neighborRod.Comp.Category.ToString());
        }

        return requirements.Count == 0;
    }

    public static float DurabilityMod(FissionRodComponent rod)
    {
        if (rod.Infinite)
            return 1;

        return Math.Clamp(1.5f * (rod.Durability / rod.MaxDurability) - 0.25f, 0.25f, 1);
    }

    private static int DurabilityLevel(FissionRodComponent rod)
    {
        if (rod.Infinite)
            return 5;

        return Math.Clamp((int) MathF.Ceiling(rod.Durability / rod.MaxDurability * 5 - 0.8f), 0, 5);
    }

    /// <summary>calc_stat_decrease: y = (x * A) + (1 - A).</summary>
    private static void CalcStatDecrease(FissionRodComponent rod)
    {
        var durability = DurabilityMod(rod);
        rod.CurrentPowerMod = rod.PowerAmpMod * durability + (1 - durability);
        rod.CurrentHeatMod = rod.HeatAmpMod * durability + (1 - durability);
    }

    private static bool Enrich(FissionRodComponent rod, float powerMod, float heatMod)
    {
        var success = false;
        if (rod.PowerEnrichResult != null && powerMod > rod.PowerEnrichThreshold && rod.PowerEnrichProgress < rod.EnrichmentCycles)
        {
            rod.PowerEnrichProgress++;
            success = true;
        }

        if (rod.HeatEnrichResult != null && heatMod > rod.HeatEnrichThreshold && rod.HeatEnrichProgress < rod.EnrichmentCycles)
        {
            rod.HeatEnrichProgress++;
            success = true;
        }

        return success;
    }

    #endregion

    private void UpdateChamberVisuals(Entity<FissionChamberComponent> ent)
    {
        var chamber = ent.Comp;
        var rod = GetRod(ent);
        var status = FissionChamberStatus.None;
        switch (chamber.State)
        {
            case FissionChamberState.Down when rod != null:
                if (chamber.Enriching)
                    status = FissionChamberStatus.Blue;
                else if (chamber.RequirementsMet)
                    status = chamber.Operational ? FissionChamberStatus.Green : FissionChamberStatus.Orange;
                else
                    status = chamber.Operational ? FissionChamberStatus.Orange : FissionChamberStatus.Red;
                break;
            case FissionChamberState.OverloadIdle:
                status = rod is { Comp.Category: FissionRodCategory.Fuel } ? FissionChamberStatus.Orange : FissionChamberStatus.Red;
                break;
            case FissionChamberState.OverloadActive:
                status = FissionChamberStatus.Overload;
                break;
        }

        _appearance.SetData(ent, FissionChamberVisuals.State, chamber.State);
        _appearance.SetData(ent, FissionChamberVisuals.Status, status);
        _appearance.SetData(ent, FissionChamberVisuals.Rod, rod is { } r ? (int) r.Comp.Category : -1);
        _appearance.SetData(ent, FissionChamberVisuals.Durability, rod != null ? chamber.DurabilityLevel : -1);
        _appearance.SetData(ent, FissionChamberVisuals.Welded, chamber.Welded);
        _appearance.SetData(ent, FissionChamberVisuals.Panel, chamber.PanelOpen);
    }
}
