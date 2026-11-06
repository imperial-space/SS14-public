using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

/// <summary>Готовая болезнь из SS13 (не собранная вирусологом).</summary>
[Prototype]
public sealed partial class DiseasePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    /// <summary>Возбудитель: вирус, бактерия, проклятие.</summary>
    [DataField]
    public LocId Agent = "disease-agent-unknown";

    [DataField] public int MaxStages = 4;

    /// <summary>Шанс перейти на следующую стадию за секунду, в процентах.</summary>
    [DataField] public float StageProbability = 2;

    /// <summary>Шанс заразить соседей по воздуху за секунду, в процентах.</summary>
    [DataField] public float Infectivity = 41;

    /// <summary>Шанс излечиться за секунду при наличии лекарства, в процентах.</summary>
    [DataField] public float CureChance = 4;

    [DataField] public DiseaseSeverity Severity = DiseaseSeverity.NonThreat;

    [DataField] public DiseaseFlags Flags = DiseaseFlags.Curable | DiseaseFlags.CanCarry | DiseaseFlags.CanResist;

    [DataField] public DiseaseSpread Spread = DiseaseSpread.Airborne | DiseaseSpread.ContactFluids | DiseaseSpread.ContactSkin;

    [DataField] public DiseaseVisibility Visibility = DiseaseVisibility.None;

    /// <summary>Реагенты-лекарства.</summary>
    [DataField] public List<ProtoId<ReagentPrototype>> Cures = new();

    /// <summary>Нужны ли все лекарства сразу.</summary>
    [DataField] public bool NeedsAllCures = true;

    /// <summary>Без этого органа болезнь не живёт.</summary>
    [DataField] public ProtoId<OrganCategoryPrototype>? RequiredOrgan;

    /// <summary>Обходит ли видовой иммунитет к вирусам.</summary>
    [DataField] public bool BypassesImmunity;

    /// <summary>Идёт ли болезнь, пока носитель мёртв.</summary>
    [DataField] public bool ProcessDead;

    [DataField] public float SpreadingModifier = 1;

    /// <summary>Симптомы, если это заранее собранная продвинутая болезнь.</summary>
    [DataField] public List<ProtoId<DiseaseSymptomPrototype>> Symptoms = new();
}
