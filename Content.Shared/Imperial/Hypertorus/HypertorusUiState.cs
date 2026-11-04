using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Hypertorus;

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
