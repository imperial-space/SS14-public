using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Ядерный стержень: топливо, замедлитель или охладитель (nuclear_rods.dm).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionRodComponent : Component
{
    [DataField(required: true)] public FissionRodCategory Category;

    /// <summary>Отрицательное значение — бесконечная прочность (INFINITY в SS13).</summary>
    [DataField] public float MaxDurability = 3000;
    [DataField] public float Durability = -1;

    [DataField] public float HeatAmount;
    [DataField] public float HeatAmpMod = 1;
    [DataField] public float PowerAmount;
    [DataField] public float PowerAmpMod = 1;
    [ViewVariables] public float CurrentHeatMod = 1;
    [ViewVariables] public float CurrentPowerMod = 1;

    /// <summary>
    /// Требования к соседям: "Fuel"/"Moderator"/"Coolant" — любой стержень категории,
    /// иначе id прототипа конкретного стержня.
    /// </summary>
    [DataField] public List<string> Requirements = new();

    [DataField] public float MinimumTempModifier;
    [DataField] public float ReactorOverheatModifier;

    /// <summary>Сила излучения вне камеры (radiation_range/chance).</summary>
    [DataField] public float RadiationIntensity;

    [DataField] public int EnrichmentCycles = 25;
    [DataField] public float PowerEnrichThreshold;
    [DataField] public float HeatEnrichThreshold;
    [DataField] public EntProtoId? PowerEnrichResult;
    [DataField] public EntProtoId? HeatEnrichResult;
    [ViewVariables] public int PowerEnrichProgress;
    [ViewVariables] public int HeatEnrichProgress;

    /// <summary>Банановый стержень: случайные характеристики при создании.</summary>
    [DataField] public bool RandomStats;

    /// <summary>Можно ли изготовить в фабрикаторе (craftable).</summary>
    [DataField] public bool Craftable;

    /// <summary>Нужен научный диск фабрикатора (upgrade_required).</summary>
    [DataField] public bool UpgradeRequired;

    /// <summary>Материалы для фабрикатора, единицы SS14 (100 = лист).</summary>
    [DataField] public Dictionary<string, int> Materials = new();

    public bool Infinite => MaxDurability < 0;
}
