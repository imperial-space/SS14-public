using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Hypertorus;

[Serializable, NetSerializable]
public enum HypertorusUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum HypertorusVisuals : byte
{
    /// <summary>Детали связаны интерфейсом (active).</summary>
    Active,

    /// <summary>Панель открыта отвёрткой (panel_open).</summary>
    Open,

    /// <summary>Порт треснул от давления модератора (cracked).</summary>
    Cracked,
}

[Serializable, NetSerializable]
public enum HypertorusVisualLayers : byte
{
    Base,
    Active,
    Crack,
}

/// <summary>Ручки интерфейса (ui_act в hfr_parts.dm).</summary>
[Serializable, NetSerializable]
public enum HypertorusParameter : byte
{
    HeatingConductor,
    MagneticConstrictor,
    CurrentDamper,
    FuelInjectionRate,
    ModeratorInjectionRate,
    CoolingVolume,
    ModeratorFilteringRate,
}

[Serializable, NetSerializable]
public enum HypertorusToggle : byte
{
    StartPower,
    StartCooling,
    StartFuel,
    StartModerator,
    WasteRemove,
}

[Serializable, NetSerializable]
public sealed class HypertorusGasEntry(Gas gas, float amount)
{
    public readonly Gas Gas = gas;
    public readonly float Amount = amount;
}

/// <summary>ui_data интерфейса HFR.</summary>
[Serializable, NetSerializable]
public sealed class HypertorusUiState : BoundUserInterfaceState
{
    public string? Selected;
    public List<HypertorusGasEntry> FusionGases = new();
    public List<HypertorusGasEntry> ModeratorGases = new();

    public double EnergyLevel;
    public double HeatLimiterModifier;
    public double HeatOutputMin;
    public double HeatOutputMax;
    public double HeatOutput;
    public double Instability;

    public float HeatingConductor;
    public float MagneticConstrictor;
    public float FuelInjectionRate;
    public float ModeratorInjectionRate;
    public float CurrentDamper;

    public int PowerLevel;
    public float ApcEnergy;
    public float IronContent;
    public float Integrity;

    public bool StartPower;
    public bool StartCooling;
    public bool StartFuel;
    public bool StartModerator;

    public float FusionTemperature;
    public float ModeratorTemperature;
    public float OutputTemperature;
    public float CoolantTemperature;
    public float FusionTemperatureArchived;
    public float ModeratorTemperatureArchived;
    public float OutputTemperatureArchived;
    public float CoolantTemperatureArchived;
    public float TemperaturePeriod = 1;

    public bool WasteRemove;
    public List<Gas> FilteredGases = new();
    public float CoolingVolume;
    public float ModeratorFilteringRate;
}

[Serializable, NetSerializable]
public sealed class HypertorusToggleMessage(HypertorusToggle toggle) : BoundUserInterfaceMessage
{
    public readonly HypertorusToggle Toggle = toggle;
}

[Serializable, NetSerializable]
public sealed class HypertorusParameterMessage(HypertorusParameter parameter, float value) : BoundUserInterfaceMessage
{
    public readonly HypertorusParameter Parameter = parameter;
    public readonly float Value = value;
}

/// <summary>«fuel»: выбор рецепта, null — «Nothing».</summary>
[Serializable, NetSerializable]
public sealed class HypertorusSelectFuelMessage(string? fuel) : BoundUserInterfaceMessage
{
    public readonly string? Fuel = fuel;
}

/// <summary>«filter»: переключить газ в фильтре модератора.</summary>
[Serializable, NetSerializable]
public sealed class HypertorusFilterMessage(Gas gas) : BoundUserInterfaceMessage
{
    public readonly Gas Gas = gas;
}

/// <summary>Заварка треснувшего порта сваркой (10 секунд).</summary>
[Serializable, NetSerializable]
public sealed partial class HypertorusRepairDoAfterEvent : SimpleDoAfterEvent;
