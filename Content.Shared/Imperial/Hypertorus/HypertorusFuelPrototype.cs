using System.Linq;
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
[Prototype]
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

    /// <summary>
    /// Газы задаются id (имя в <see cref="Gas"/>): гелий есть только в закрытой сборке,
    /// поэтому газ, которого нет в текущей сборке, просто пропускается.
    /// </summary>
    public static Gas? ResolveGas(string id)
    {
        return Enum.TryParse<Gas>(id, out var gas) && Enum.IsDefined(gas) && (int) gas < Atmospherics.TotalNumberOfGases
            ? gas
            : null;
    }

    /// <summary>Два газа-топлива.</summary>
    [DataField("requirements", required: true)]
    public List<string> RequirementIds = new();

    /// <summary>Побочные продукты синтеза в смеси топлива.</summary>
    [DataField("primaryProducts", required: true)]
    public List<string> PrimaryProductIds = new();

    /// <summary>Шесть ступеней газов, которые выходят в модератор по уровням синтеза.</summary>
    [DataField("secondaryProducts", required: true)]
    public List<string> SecondaryProductIds = new();

    private List<Gas>? _requirements;
    private List<Gas>? _primaryProducts;

    /// <summary>Газы-топливо, которые есть в сборке.</summary>
    public List<Gas> Requirements => _requirements ??= RequirementIds.Select(ResolveGas).OfType<Gas>().ToList();

    /// <summary>Побочные продукты, которые есть в сборке.</summary>
    public List<Gas> PrimaryProducts => _primaryProducts ??= PrimaryProductIds.Select(ResolveGas).OfType<Gas>().ToList();

    /// <summary>Побочный продукт по номеру из рецепта; null, если газа нет в сборке.</summary>
    public Gas? PrimaryProduct(int index) => index < PrimaryProductIds.Count ? ResolveGas(PrimaryProductIds[index]) : null;

    /// <summary>Газ ступени (0–5) по номеру из рецепта; null, если газа нет в сборке.</summary>
    public Gas? SecondaryProduct(int index) => index < SecondaryProductIds.Count ? ResolveGas(SecondaryProductIds[index]) : null;

    [DataField]
    public HypertorusMeltdownFlags MeltdownFlags = HypertorusMeltdownFlags.BaseExplosion;
}
