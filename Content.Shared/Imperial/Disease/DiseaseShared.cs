using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

// Вирусология 1 в 1 с SS13 (code/datums/diseases).

/// <summary>DISEASE_SPREAD_*: как болезнь передаётся между носителями.</summary>
[Flags, Serializable, NetSerializable]
public enum DiseaseSpread : byte
{
    None = 0,

    /// <summary>Особый способ передачи, прописанный самой болезнью.</summary>
    Special = 1 << 0,

    /// <summary>Не передаётся.</summary>
    NonContagious = 1 << 1,

    /// <summary>Через кровь: укол, переливание.</summary>
    Blood = 1 << 2,

    /// <summary>Через жидкости: еда, питьё, брызги.</summary>
    ContactFluids = 1 << 3,

    /// <summary>Через кожу: прикосновение.</summary>
    ContactSkin = 1 << 4,

    /// <summary>По воздуху.</summary>
    Airborne = 1 << 5,
}

/// <summary>Флаги болезни: лечится ли, можно ли быть носителем, вырабатывается ли иммунитет.</summary>
[Flags, Serializable, NetSerializable]
public enum DiseaseFlags : byte
{
    None = 0,
    Curable = 1 << 0,
    CanCarry = 1 << 1,
    CanResist = 1 << 2,

    /// <summary>Хроническая: лекарство лишь откатывает стадию.</summary>
    Chronic = 1 << 3,

    /// <summary>Лекарство снимает по одной стадии, а не лечит сразу.</summary>
    IncrementalCure = 1 << 4,
}

/// <summary>HIDDEN_*: от чего болезнь скрыта.</summary>
[Flags, Serializable, NetSerializable]
public enum DiseaseVisibility : byte
{
    None = 0,

    /// <summary>Не видна анализатору здоровья.</summary>
    Scanner = 1 << 0,

    /// <summary>Не видна в PanD.E.M.I.C.</summary>
    Pandemic = 1 << 1,
}

/// <summary>DISEASE_SEVERITY_*.</summary>
[Serializable, NetSerializable]
public enum DiseaseSeverity : byte
{
    Uncurable,
    Positive,
    NonThreat,
    Minor,
    Medium,
    Harmful,
    Dangerous,
    Biohazard,
}

/// <summary>Порог симптома для окна PanD.E.M.I.C. (threshold_descs).</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class DiseaseThreshold
{
    /// <summary>Какое свойство нужно набрать.</summary>
    [DataField(required: true)]
    public DiseaseProperty Property;

    [DataField(required: true)]
    public int Value;

    [DataField(required: true)]
    public LocId Description;
}

[Serializable, NetSerializable]
public enum DiseaseProperty : byte
{
    Resistance,
    Stealth,
    StageSpeed,
    Transmittable,
    Severity,
}

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

#region PanD.E.M.I.C.

[Serializable, NetSerializable]
public enum PandemicUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PandemicSymptomData
{
    public string Name { get; }
    public string Description { get; }
    public int Stealth { get; }
    public int Resistance { get; }
    public int StageSpeed { get; }
    public int Transmission { get; }
    public int Level { get; }
    public string Cure { get; }
    public string[] Thresholds { get; }

    public PandemicSymptomData(string name, string description, int stealth, int resistance, int stageSpeed,
        int transmission, int level, string cure, string[] thresholds)
    {
        Name = name;
        Description = description;
        Stealth = stealth;
        Resistance = resistance;
        StageSpeed = stageSpeed;
        Transmission = transmission;
        Level = level;
        Cure = cure;
        Thresholds = thresholds;
    }
}

[Serializable, NetSerializable]
public sealed class PandemicDiseaseData
{
    public int Index { get; }
    public string Name { get; }
    public string Agent { get; }
    public string Description { get; }
    public string Spread { get; }
    public string CureText { get; }
    public bool Advanced { get; }
    public bool CanRename { get; }
    public int Resistance { get; }
    public int StageSpeed { get; }
    public int Stealth { get; }
    public int Transmission { get; }
    public int Severity { get; }
    public PandemicSymptomData[] Symptoms { get; }

    public PandemicDiseaseData(int index, string name, string agent, string description, string spread, string cureText,
        bool advanced, bool canRename, int resistance, int stageSpeed, int stealth, int transmission, int severity,
        PandemicSymptomData[] symptoms)
    {
        Index = index;
        Name = name;
        Agent = agent;
        Description = description;
        Spread = spread;
        CureText = cureText;
        Advanced = advanced;
        CanRename = canRename;
        Resistance = resistance;
        StageSpeed = stageSpeed;
        Stealth = stealth;
        Transmission = transmission;
        Severity = severity;
        Symptoms = symptoms;
    }
}

[Serializable, NetSerializable]
public sealed class PandemicResistanceData
{
    public string Id { get; }
    public string Name { get; }

    public PandemicResistanceData(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class PandemicBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Ready { get; }
    public bool HasBeaker { get; }
    public bool HasBlood { get; }
    public float Volume { get; }
    public float Capacity { get; }
    public string Dna { get; }
    public string BloodType { get; }
    public PandemicDiseaseData[] Diseases { get; }
    public PandemicResistanceData[] Resistances { get; }

    public PandemicBoundUserInterfaceState(bool ready, bool hasBeaker, bool hasBlood, float volume, float capacity,
        string dna, string bloodType, PandemicDiseaseData[] diseases, PandemicResistanceData[] resistances)
    {
        Ready = ready;
        HasBeaker = hasBeaker;
        HasBlood = hasBlood;
        Volume = volume;
        Capacity = capacity;
        Dna = dna;
        BloodType = bloodType;
        Diseases = diseases;
        Resistances = resistances;
    }
}

[Serializable, NetSerializable]
public sealed class PandemicEjectBeakerMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class PandemicEmptyBeakerMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class PandemicCreateCultureMessage : BoundUserInterfaceMessage
{
    public int Index { get; }

    public PandemicCreateCultureMessage(int index)
    {
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed class PandemicCreateVaccineMessage : BoundUserInterfaceMessage
{
    public string Id { get; }

    public PandemicCreateVaccineMessage(string id)
    {
        Id = id;
    }
}

[Serializable, NetSerializable]
public sealed class PandemicRenameDiseaseMessage : BoundUserInterfaceMessage
{
    public int Index { get; }
    public string Name { get; }

    public PandemicRenameDiseaseMessage(int index, string name)
    {
        Index = index;
        Name = name;
    }
}

#endregion
