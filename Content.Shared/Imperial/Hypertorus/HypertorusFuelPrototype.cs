using Content.Shared.Atmos;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Hypertorus;

/// <summary>Флаги аварии гиперторуса (HYPERTORUS_FLAG_* в _hfr_defines.dm).</summary>
[Flags]
public enum HypertorusMeltdownFlags
{
    None = 0,
    BaseExplosion = 1 << 0,
    MediumExplosion = 1 << 1,
    DevastatingExplosion = 1 << 2,
    RadiationPulse = 1 << 3,
    Emp = 1 << 4,
    MinimumSpread = 1 << 5,
    MediumSpread = 1 << 6,
    BigSpread = 1 << 7,
    MassiveSpread = 1 << 8,
    CriticalMeltdown = 1 << 9,
}

/// <summary>
/// Рецепт топлива гиперторуса (datum/hfr_fuel в hfr_fuel_datums.dm).
/// Газы SS13, которых нет в SS14, заменены близкими по смыслу: фреон — фрезон, плюоксий — озон,
/// прото-нитрат — фазон, нитрий — термониум, заукер и миазмы — аммиак, хилиум — оксид азота, галон — CO₂.
/// </summary>
[Prototype("hypertorusFuel")]
public sealed partial class HypertorusFuelPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>Порядок в списке рецептов (как порядок подтипов в SS13).</summary>
    [DataField]
    public int Order;

    [DataField]
    public float NegativeTemperatureMultiplier = 1;

    [DataField]
    public float PositiveTemperatureMultiplier = 1;

    [DataField]
    public float EnergyConcentrationMultiplier = 1;

    [DataField]
    public float FuelConsumptionMultiplier = 1;

    [DataField]
    public float GasProductionMultiplier = 1;

    /// <summary>Не больше 1 (min(temperature_change_multiplier, 1) в New()).</summary>
    [DataField]
    public float TemperatureChangeMultiplier = 1;

    /// <summary>Два газа-топлива.</summary>
    [DataField(required: true)]
    public List<Gas> Requirements = new();

    /// <summary>Побочные продукты синтеза в смеси топлива.</summary>
    [DataField(required: true)]
    public List<Gas> PrimaryProducts = new();

    /// <summary>Шесть ступеней газов, которые выходят в модератор по уровням синтеза.</summary>
    [DataField(required: true)]
    public List<Gas> SecondaryProducts = new();

    [DataField]
    public HypertorusMeltdownFlags MeltdownFlags = HypertorusMeltdownFlags.BaseExplosion;
}
