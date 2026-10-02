using Robust.Shared.Random;
using Content.Server.AlertLevel;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared.Burial.Components;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Fission;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Fission;

public sealed partial class FissionSystem
{
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    private const string PlasteelStack = "Plasteel";
    private const string SteelSheet = "SheetSteel1";
    private const string HvCableStack = "CableHV";

    private void InitializeRepair()
    {
        SubscribeLocalEvent<FissionReactorComponent, InteractUsingEvent>(OnReactorInteractUsing);
        SubscribeLocalEvent<FissionReactorComponent, FissionDoAfterEvent>(OnReactorDoAfter);
    }

    private void OnReactorInteractUsing(Entity<FissionReactorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var reactor = ent.Comp;
        var user = args.User;
        var used = args.Used;

        // Лопатой выгребается расплавленный кориум.
        if (HasComp<ShovelComponent>(used) && reactor.Broken && reactor.RepairStep == FissionRepairStep.Digging)
        {
            args.Handled = true;
            StartDoAfter(user, ent, TimeSpan.FromSeconds(3), FissionDoAfterAction.ReactorDig, used);
            return;
        }

        if (TryComp<StackComponent>(used, out var stack))
        {
            if (stack.StackTypeId == HvCableStack)
            {
                args.Handled = true;
                TryStartTerminal(ent, user, used, stack);
                return;
            }

            if (stack.StackTypeId == PlasteelStack)
            {
                args.Handled = true;
                OnPlasteelUsed(ent, user, used, stack);
                return;
            }
        }

        if (_tool.HasQuality(used, PryingQuality))
        {
            if (reactor.Broken && reactor.RepairStep == FissionRepairStep.Crowbar)
            {
                args.Handled = true;
                _tool.UseTool(used, user, ent, 1, PryingQuality, new FissionDoAfterEvent(FissionDoAfterAction.ReactorCrowbar));
            }
            else if (!reactor.Broken && reactor.Venting)
            {
                args.Handled = true;
                _tool.UseTool(used, user, ent, 8, PryingQuality, new FissionDoAfterEvent(FissionDoAfterAction.ReactorCloseVent));
            }

            return;
        }

        if (_tool.HasQuality(used, AnchoringQuality))
        {
            if (reactor.Broken && reactor.RepairStep == FissionRepairStep.Wrench)
            {
                args.Handled = true;
                _tool.UseTool(used, user, ent, 1, AnchoringQuality, new FissionDoAfterEvent(FissionDoAfterAction.ReactorWrench));
            }
            else if (!reactor.Broken && reactor.ControlRodsRemaining < FissionReactorComponent.TotalControlRods)
            {
                args.Handled = true;
                _tool.UseTool(used, user, ent, 8, AnchoringQuality, new FissionDoAfterEvent(FissionDoAfterAction.ReactorControlRod));
            }

            return;
        }

        if (_tool.HasQuality(used, WeldingQuality) && reactor.Broken && reactor.RepairStep == FissionRepairStep.Welding)
        {
            args.Handled = true;
            _tool.UseTool(used, user, ent, 1, WeldingQuality, new FissionDoAfterEvent(FissionDoAfterAction.ReactorWeld), fuel: 1);
            return;
        }

        if (_tool.HasQuality(used, ScrewingQuality) && reactor.Broken && reactor.RepairStep == FissionRepairStep.Screwdriver)
        {
            args.Handled = true;
            _tool.UseTool(used, user, ent, 1, ScrewingQuality, new FissionDoAfterEvent(FissionDoAfterAction.ReactorScrew));
        }
    }

    /// <summary>
    /// Пластитан SS13 заменён пласталью: шаг нового сердечника, шаг защитного кожуха
    /// и заплатка целостности выключенного реактора (со сваркой во второй руке).
    /// </summary>
    private void OnPlasteelUsed(Entity<FissionReactorComponent> ent, EntityUid user, EntityUid used, StackComponent stack)
    {
        var reactor = ent.Comp;
        if (stack.Count < 5)
        {
            _popup.PopupEntity(Loc.GetString("fission-reactor-need-sheets"), ent, user);
            return;
        }

        if (reactor.Broken)
        {
            if (reactor.RepairStep == FissionRepairStep.Plastitanium)
                StartDoAfter(user, ent, TimeSpan.FromSeconds(3), FissionDoAfterAction.ReactorPlastitanium, used);
            else if (reactor.RepairStep == FissionRepairStep.Plasteel)
                StartDoAfter(user, ent, TimeSpan.FromSeconds(3), FissionDoAfterAction.ReactorPlasteel, used);
            return;
        }

        if (!reactor.Offline)
        {
            _popup.PopupEntity(Loc.GetString("fission-reactor-must-be-off"), ent, user);
            return;
        }

        if (reactor.Damage <= 0)
        {
            _popup.PopupEntity(Loc.GetString("fission-reactor-nothing-to-repair"), ent, user);
            return;
        }

        EntityUid? welder = null;
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (held != used && _tool.HasQuality(held, WeldingQuality))
                welder = held;
        }

        if (welder == null || !TryComp<ToolComponent>(welder, out _))
        {
            _popup.PopupEntity(Loc.GetString("fission-reactor-need-welder"), ent, user);
            return;
        }

        _tool.UseTool(welder.Value, user, ent, 4, WeldingQuality, new FissionDoAfterEvent(FissionDoAfterAction.ReactorPatch), fuel: 1);
    }

    /// <summary>Сборка выходного терминала из 10 отрезков ВВ кабеля на клетке между ядром и инженером.</summary>
    private void TryStartTerminal(Entity<FissionReactorComponent> ent, EntityUid user, EntityUid used, StackComponent stack)
    {
        if (ent.Comp.Broken)
            return;

        if (stack.Count < 10)
        {
            _popup.PopupEntity(Loc.GetString("fission-reactor-need-cable"), ent, user);
            return;
        }

        if (!TryGetTile(ent, out var grid, out var center))
            return;

        var userTile = _map.TileIndicesFor(grid.Value, grid.Value, Transform(user).Coordinates);
        var delta = userTile - center;
        var step = new Vector2i(Math.Sign(delta.X), Math.Sign(delta.Y));
        if (step.X != 0 && step.Y != 0)
            step = Math.Abs(delta.X) >= Math.Abs(delta.Y) ? new Vector2i(step.X, 0) : new Vector2i(0, step.Y);
        if (step == Vector2i.Zero)
            return;

        var tile = center + step * 2;
        foreach (var anchored in _map.GetAnchoredEntities(grid.Value, tile))
        {
            if (HasComp<FissionPowerTerminalComponent>(anchored))
            {
                _popup.PopupEntity(Loc.GetString("fission-reactor-already-wired"), ent, user);
                return;
            }
        }

        _popup.PopupEntity(Loc.GetString("fission-reactor-wiring"), ent, user);
        var coords = GetNetCoordinates(_map.GridTileToLocal(grid.Value, grid.Value, tile));
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(2),
            new FissionDoAfterEvent(FissionDoAfterAction.ReactorTerminal, coords), ent, ent, used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnReactorDoAfter(Entity<FissionReactorComponent> ent, ref FissionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var reactor = ent.Comp;
        var user = args.User;
        var userCoords = Transform(user).Coordinates;

        switch (args.Action)
        {
            case FissionDoAfterAction.ReactorDig:
                if (!reactor.Broken || reactor.RepairStep != FissionRepairStep.Digging)
                    return;
                Spawn(reactor.Slag, Transform(ent).Coordinates);
                if (_random.Prob(0.2f))
                {
                    reactor.RepairStep++;
                    _popup.PopupEntity(Loc.GetString("fission-repair-dig-done"), ent, user);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("fission-repair-dig-more"), ent, user);
                }
                break;

            case FissionDoAfterAction.ReactorCrowbar:
                if (!reactor.Broken || reactor.RepairStep != FissionRepairStep.Crowbar)
                    return;
                reactor.RepairStep++;
                Spawn(SteelSheet, userCoords);
                Spawn(SteelSheet, userCoords);
                _popup.PopupEntity(Loc.GetString("fission-repair-crowbar"), ent, user);
                break;

            case FissionDoAfterAction.ReactorPlastitanium:
                if (!reactor.Broken || reactor.RepairStep != FissionRepairStep.Plastitanium || !UseSheets(args.Used, 5))
                    return;
                reactor.RepairStep++;
                _popup.PopupEntity(Loc.GetString("fission-repair-plastitanium"), ent, user);
                break;

            case FissionDoAfterAction.ReactorWrench:
                if (!reactor.Broken || reactor.RepairStep != FissionRepairStep.Wrench)
                    return;
                reactor.RepairStep++;
                Spawn(SteelSheet, userCoords);
                Spawn(SteelSheet, userCoords);
                _popup.PopupEntity(Loc.GetString("fission-repair-wrench"), ent, user);
                break;

            case FissionDoAfterAction.ReactorWeld:
                if (!reactor.Broken || reactor.RepairStep != FissionRepairStep.Welding)
                    return;
                reactor.RepairStep++;
                Spawn(SteelSheet, userCoords);
                Spawn(SteelSheet, userCoords);
                _popup.PopupEntity(Loc.GetString("fission-repair-weld"), ent, user);
                break;

            case FissionDoAfterAction.ReactorPlasteel:
                if (!reactor.Broken || reactor.RepairStep != FissionRepairStep.Plasteel || !UseSheets(args.Used, 5))
                    return;
                reactor.RepairStep++;
                _popup.PopupEntity(Loc.GetString("fission-repair-plasteel"), ent, user);
                break;

            case FissionDoAfterAction.ReactorScrew:
                if (!reactor.Broken || reactor.RepairStep != FissionRepairStep.Screwdriver)
                    return;
                _popup.PopupEntity(Loc.GetString("fission-repair-screw"), ent, user);
                SetFixed(ent);
                break;

            case FissionDoAfterAction.ReactorPatch:
            {
                if (reactor.Broken || !reactor.Offline)
                    return;

                EntityUid? sheets = null;
                foreach (var held in _hands.EnumerateHeld(user))
                {
                    if (TryComp<StackComponent>(held, out var stack) && stack.StackTypeId == PlasteelStack)
                        sheets = held;
                }

                if (!UseSheets(sheets, 5))
                    return;

                AdjustDamage(ent, -FissionReactorComponent.MeltdownPoint * 0.1f);
                break;
            }

            case FissionDoAfterAction.ReactorCloseVent:
                if (!reactor.Broken)
                    reactor.Venting = false;
                break;

            case FissionDoAfterAction.ReactorControlRod:
                if (!reactor.Broken && reactor.ControlRodsRemaining < FissionReactorComponent.TotalControlRods)
                    reactor.ControlRodsRemaining++;
                break;

            case FissionDoAfterAction.ReactorTerminal:
            {
                if (args.Location is not { } netCoords || !UseSheets(args.Used, 10))
                    return;

                var coords = GetCoordinates(netCoords);
                var terminal = Spawn(reactor.Terminal, coords);
                _xform.AnchorEntity(terminal);
                if (TryComp<FissionPowerTerminalComponent>(terminal, out var terminalComp))
                    terminalComp.Reactor = ent;
                _popup.PopupEntity(Loc.GetString("fission-reactor-wired"), ent, user);
                break;
            }
        }

        UpdateReactorVisuals(ent);
    }

    private bool UseSheets(EntityUid? used, int amount)
    {
        if (used is not { } uid || !TryComp<StackComponent>(uid, out var stack) || stack.Count < amount)
            return false;

        return _stack.TryUse((uid, stack), amount);
    }
}
