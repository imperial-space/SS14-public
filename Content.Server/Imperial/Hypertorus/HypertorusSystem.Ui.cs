using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.Imperial.Hypertorus;
using Content.Shared.Power.Components;

namespace Content.Server.Imperial.Hypertorus;

/// <summary>Интерфейс HFR (ui_data и ui_act в hfr_parts.dm).</summary>
public sealed partial class HypertorusSystem
{
    private void InitializeUi()
    {
        Subs.BuiEvents<HypertorusPartComponent>(HypertorusUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<HypertorusToggleMessage>(OnToggle);
            subs.Event<HypertorusParameterMessage>(OnParameter);
            subs.Event<HypertorusSelectFuelMessage>(OnSelectFuel);
            subs.Event<HypertorusFilterMessage>(OnFilter);
        });
    }

    private bool TryGetCore(Entity<HypertorusPartComponent> iface, out Entity<HypertorusCoreComponent> core)
    {
        core = default;
        if (iface.Comp.Core is not { } uid || !TryComp<HypertorusCoreComponent>(uid, out var comp) || !comp.Active)
            return false;

        core = (uid, comp);
        return true;
    }

    private void OnUiOpened(Entity<HypertorusPartComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (TryGetCore(ent, out var core))
            UpdateUi(core);
    }

    private void OnToggle(Entity<HypertorusPartComponent> ent, ref HypertorusToggleMessage args)
    {
        if (!TryGetCore(ent, out var core))
            return;

        var c = core.Comp;
        switch (args.Toggle)
        {
            // Пока идёт синтез, питание не выключить.
            case HypertorusToggle.StartPower when c.PowerLevel == 0:
                c.StartPower = !c.StartPower;
                break;
            case HypertorusToggle.StartCooling when c.StartPower && !c.StartFuel && !c.StartModerator
                                                    && !(c.StartCooling && c.PowerLevel > 0):
                c.StartCooling = !c.StartCooling;
                break;
            case HypertorusToggle.StartFuel when c.StartPower && c.StartCooling:
                c.StartFuel = !c.StartFuel;
                break;
            case HypertorusToggle.StartModerator when c.StartPower && c.StartCooling:
                c.StartModerator = !c.StartModerator;
                break;
            case HypertorusToggle.WasteRemove:
                c.WasteRemove = !c.WasteRemove;
                break;
        }

        CheckPowerUse(core);
        UpdateUi(core);
    }

    private void OnParameter(Entity<HypertorusPartComponent> ent, ref HypertorusParameterMessage args)
    {
        if (!TryGetCore(ent, out var core) || !float.IsFinite(args.Value))
            return;

        var c = core.Comp;
        var v = args.Value;
        switch (args.Parameter)
        {
            case HypertorusParameter.HeatingConductor:
                c.HeatingConductor = Math.Clamp(v, 50, 500);
                break;
            case HypertorusParameter.MagneticConstrictor:
                c.MagneticConstrictor = Math.Clamp(v, 50, 1000);
                break;
            case HypertorusParameter.FuelInjectionRate:
                c.FuelInjectionRate = Math.Clamp(v, 0.5f, 150);
                break;
            case HypertorusParameter.ModeratorInjectionRate:
                c.ModeratorInjectionRate = Math.Clamp(v, 0.5f, 150);
                break;
            case HypertorusParameter.CurrentDamper:
                c.CurrentDamper = Math.Clamp(v, 0, 1000);
                break;
            case HypertorusParameter.ModeratorFilteringRate:
                c.ModeratorFilteringRate = Math.Clamp(v, 5, 200);
                break;
            case HypertorusParameter.CoolingVolume:
                c.CoolingVolume = Math.Clamp(v, 50, 2000);
                break;
        }

        UpdateUi(core);
    }

    /// <summary>«fuel»: смена рецепта сбрасывает смесь топлива в порт отходов (dump_gases).</summary>
    private void OnSelectFuel(Entity<HypertorusPartComponent> ent, ref HypertorusSelectFuelMessage args)
    {
        if (!TryGetCore(ent, out var core) || core.Comp.PowerLevel > 0)
            return;

        core.Comp.SelectedFuel = args.Fuel != null && _proto.HasIndex<HypertorusFuelPrototype>(args.Fuel) ? args.Fuel : null;
        if (core.Comp.InternalFusion.TotalMoles > 0 && GetPipeAir(core.Comp.WasteOutput) is { } output)
            _atmos.Merge(output, RemoveRatioAt(core.Comp.InternalFusion, core.Comp.FusionTemp, 1));

        UpdateUi(core);
    }

    private void OnFilter(Entity<HypertorusPartComponent> ent, ref HypertorusFilterMessage args)
    {
        if (!TryGetCore(ent, out var core) || !Enum.IsDefined(args.Gas))
            return;

        if (!core.Comp.ModeratorScrubbing.Remove(args.Gas))
            core.Comp.ModeratorScrubbing.Add(args.Gas);

        UpdateUi(core);
    }

    private void UpdateUi(Entity<HypertorusCoreComponent> core)
    {
        var c = core.Comp;
        if (c.Interface is not { } iface || !_ui.IsUiOpen(iface, HypertorusUiKey.Key))
            return;

        var state = new HypertorusUiState
        {
            Selected = c.SelectedFuel,
            EnergyLevel = c.Energy,
            HeatLimiterModifier = c.HeatLimiterModifier,
            HeatOutputMin = c.HeatOutputMin,
            HeatOutputMax = c.HeatOutputMax,
            HeatOutput = c.HeatOutput,
            Instability = c.Instability,
            HeatingConductor = c.HeatingConductor,
            MagneticConstrictor = c.MagneticConstrictor,
            FuelInjectionRate = c.FuelInjectionRate,
            ModeratorInjectionRate = c.ModeratorInjectionRate,
            CurrentDamper = c.CurrentDamper,
            PowerLevel = c.PowerLevel,
            ApcEnergy = GetAreaCellPercent(core),
            IronContent = c.IronContent,
            Integrity = GetIntegrityPercent(c),
            StartPower = c.StartPower,
            StartCooling = c.StartCooling,
            StartFuel = c.StartFuel,
            StartModerator = c.StartModerator,
            FusionTemperature = c.FusionTemperature,
            ModeratorTemperature = c.ModeratorTemperature,
            OutputTemperature = c.OutputTemperature,
            CoolantTemperature = c.CoolantTemperature,
            FusionTemperatureArchived = c.FusionTemperatureArchived,
            ModeratorTemperatureArchived = c.ModeratorTemperatureArchived,
            OutputTemperatureArchived = c.OutputTemperatureArchived,
            CoolantTemperatureArchived = c.CoolantTemperatureArchived,
            TemperaturePeriod = c.TemperaturePeriod,
            WasteRemove = c.WasteRemove,
            FilteredGases = new List<Gas>(c.ModeratorScrubbing),
            CoolingVolume = c.CoolingVolume,
            ModeratorFilteringRate = c.ModeratorFilteringRate,
        };

        foreach (var gas in AllGases)
        {
            var fusion = c.InternalFusion.GetMoles(gas);
            if (fusion > 0)
                state.FusionGases.Add(new HypertorusGasEntry(gas, MathF.Round(fusion, 2)));

            var moderator = c.ModeratorInternal.GetMoles(gas);
            if (moderator > 0)
                state.ModeratorGases.Add(new HypertorusGasEntry(gas, MathF.Round(moderator, 2)));
        }

        _ui.SetUiState(iface, HypertorusUiKey.Key, state);
    }

    /// <summary>get_area_cell_percent: заряд батареи APC, от которого питается ядро.</summary>
    private float GetAreaCellPercent(EntityUid core)
    {
        if (!TryComp<ApcPowerReceiverComponent>(core, out var receiver) || receiver.Provider?.Owner is not { } apc
            || !HasComp<BatteryComponent>(apc))
        {
            return 0;
        }

        return _battery.GetChargeLevel(apc) * 100;
    }
}
