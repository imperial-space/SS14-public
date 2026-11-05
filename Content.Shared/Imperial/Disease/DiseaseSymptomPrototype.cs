using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

/// <summary>datum/symptom: симптом продвинутой болезни.</summary>
[Prototype]
public sealed partial class DiseaseSymptomPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>Как болезнь с этим симптомом называют в народе.</summary>
    [DataField]
    public LocId? Illness;

    // Влияние на свойства болезни.
    [DataField] public int Stealth;
    [DataField] public int Resistance;
    [DataField] public int StageSpeed;
    [DataField] public int Transmittable;

    /// <summary>Чем выше уровень, тем сложнее получить симптом случайно.</summary>
    [DataField] public int Level;

    [DataField] public int Severity;

    /// <summary>Шанс предупреждающего сообщения, в процентах.</summary>
    [DataField] public float MessageChance = 10;

    /// <summary>Задержка между срабатываниями, в секундах.</summary>
    [DataField] public float DelayMin = 1;

    [DataField] public float DelayMax = 1;

    /// <summary>Без этого органа симптом не работает.</summary>
    [DataField] public ProtoId<OrganCategoryPrototype>? RequiredOrgan;

    /// <summary>Реагент, подавляющий симптом. Он же приближает излечение болезни.</summary>
    [DataField] public ProtoId<ReagentPrototype>? Cure = "Spaceacillin";

    /// <summary>Может ли симптом появиться при случайной генерации.</summary>
    [DataField] public bool NaturallyOccurring = true;

    /// <summary>Пороги свойств, открывающие дополнительные эффекты.</summary>
    [DataField] public List<DiseaseThreshold> Thresholds = new();
}
