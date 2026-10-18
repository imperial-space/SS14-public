using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>datum/symptom: экземпляр симптома внутри болезни.</summary>
public sealed class SymptomInstance
{
    public ProtoId<DiseaseSymptomPrototype> Proto;

    /// <summary>Кастрированный симптом влияет только на характеристики.</summary>
    public bool Neutered;

    /// <summary>Симптом подавлен своим лекарством и приближает излечение.</summary>
    public bool Remedied;

    /// <summary>Когда симптом сработает в следующий раз.</summary>
    public TimeSpan NextActivation;

    /// <summary>power: множитель силы, выставляется по порогам при старте.</summary>
    public float Power = 1;

    /// <summary>Пороговые флаги, вычисленные при старте болезни.</summary>
    public HashSet<string> Unlocked = new();

    /// <summary>Переопределение задержек по порогам.</summary>
    public float? DelayMin;
    public float? DelayMax;

    /// <summary>suppress_warning: ранние предупреждения скрыты.</summary>
    public bool SuppressWarning;

    public float MessageChance = -1;

    public SymptomInstance(ProtoId<DiseaseSymptomPrototype> proto)
    {
        Proto = proto;
    }

    public SymptomInstance Copy()
    {
        return new SymptomInstance(Proto) { Neutered = Neutered };
    }
}

/// <summary>datum/disease: экземпляр болезни на носителе или в образце.</summary>
public sealed class DiseaseInstance
{
    /// <summary>Прототип готовой болезни; null — собранная вирусологом.</summary>
    public ProtoId<DiseasePrototype>? Proto;

    public string Name = "Неизвестно";
    public LocId Agent = "disease-agent-unknown";
    public LocId? Description;

    public int Stage = 1;
    public int MaxStages = 5;
    public float StageProbability = 2;
    public float Infectivity = 41;
    public float CureChance = 4;
    public float SpreadingModifier = 1;

    public DiseaseSeverity Severity = DiseaseSeverity.NonThreat;
    public DiseaseFlags Flags = DiseaseFlags.Curable | DiseaseFlags.CanCarry | DiseaseFlags.CanResist;
    public DiseaseSpread Spread = DiseaseSpread.Airborne | DiseaseSpread.ContactFluids | DiseaseSpread.ContactSkin;
    public DiseaseVisibility Visibility;

    public List<ProtoId<ReagentPrototype>> Cures = new();
    public bool NeedsAllCures = true;
    public ProtoId<OrganCategoryPrototype>? RequiredOrgan;
    public bool BypassesImmunity;
    public bool ProcessDead;

    /// <summary>Носитель не проявляет симптомов, но заражает.</summary>
    public bool Carrier;

    /// <summary>Продвинутая болезнь: собрана из симптомов.</summary>
    public bool Advanced;

    /// <summary>Можно ли менять её симптомы вирусологией.</summary>
    public bool Mutable = true;

    public List<SymptomInstance> Symptoms = new();

    /// <summary>properties: суммарные характеристики продвинутой болезни.</summary>
    public int Resistance;
    public int Stealth;
    public int StageRate;
    public int Transmittable;
    public int SeverityValue;

    // Рабочее состояние.
    public bool Started;
    public bool StagePeaked;
    public float PeakedCycles;
    public int SymptomOffsets;
    public int ChemicalOffsets;
    public TimeSpan NextTick;

    /// <summary>Пока инкубация не прошла, болезнь скрыта от анализаторов.</summary>
    public TimeSpan? IncubationUntil;

    /// <summary>GetDiseaseID(): уникальный ключ по набору симптомов.</summary>
    public string GetId()
    {
        if (!Advanced)
            return Proto?.Id ?? Name;

        var parts = new List<string>();
        foreach (var symptom in Symptoms)
        {
            parts.Add(symptom.Neutered ? symptom.Proto.Id + "N" : symptom.Proto.Id);
        }

        parts.Sort(StringComparer.Ordinal);
        return string.Join(":", parts);
    }

    /// <summary>Copy(): новая копия, начинающая с первой стадии.</summary>
    public DiseaseInstance Copy()
    {
        var copy = new DiseaseInstance
        {
            Proto = Proto,
            Name = Name,
            Agent = Agent,
            Description = Description,
            MaxStages = MaxStages,
            StageProbability = StageProbability,
            Infectivity = Infectivity,
            CureChance = CureChance,
            SpreadingModifier = SpreadingModifier,
            Severity = Severity,
            Flags = Flags,
            Spread = Spread,
            Visibility = Visibility,
            Cures = new List<ProtoId<ReagentPrototype>>(Cures),
            NeedsAllCures = NeedsAllCures,
            RequiredOrgan = RequiredOrgan,
            BypassesImmunity = BypassesImmunity,
            ProcessDead = ProcessDead,
            Carrier = Carrier,
            Advanced = Advanced,
            Mutable = Mutable,
            Resistance = Resistance,
            Stealth = Stealth,
            StageRate = StageRate,
            Transmittable = Transmittable,
            SeverityValue = SeverityValue,
            IncubationUntil = IncubationUntil,
        };

        foreach (var symptom in Symptoms)
        {
            copy.Symptoms.Add(symptom.Copy());
        }

        return copy;
    }
}

/// <summary>mob/living/diseases: болезни, которыми болеет существо.</summary>
[RegisterComponent]
public sealed partial class DiseasedComponent : Component
{
    [ViewVariables]
    public List<DiseaseInstance> Diseases = new();
}

/// <summary>disease_resistances: приобретённый иммунитет.</summary>
[RegisterComponent]
public sealed partial class DiseaseImmunityComponent : Component
{
    [ViewVariables]
    public HashSet<string> Resistances = new();
}

/// <summary>TRAIT_VIRUSIMMUNE: вид или имплант, не болеющий вирусами.</summary>
[RegisterComponent]
public sealed partial class DiseaseVirusImmuneComponent : Component;

/// <summary>Контейнер с образцом крови: болезни и антитела внутри.</summary>
[RegisterComponent]
public sealed partial class DiseaseSampleComponent : Component
{
    [ViewVariables]
    public List<DiseaseInstance> Diseases = new();

    [ViewVariables]
    public HashSet<string> Resistances = new();

    /// <summary>Чья кровь в образце.</summary>
    [ViewVariables]
    public string? Dna;

    [ViewVariables]
    public string? BloodType;
}

/// <summary>Вакцина: при попадании в кровь даёт иммунитет к указанным болезням.</summary>
[RegisterComponent]
public sealed partial class DiseaseVaccineComponent : Component
{
    [DataField]
    public List<string> Diseases = new();
}

/// <summary>Глухота от симптома (TRAIT_DEAF).</summary>
[RegisterComponent]
public sealed partial class DiseaseDeafComponent : Component;

/// <summary>Симптом «Самодыхание»: носителю не нужен воздух.</summary>
[RegisterComponent]
public sealed partial class DiseaseNoBreathComponent : Component;

/// <summary>PanD.E.M.I.C 2200.</summary>
[RegisterComponent]
public sealed partial class PandemicComponent : Component
{
    /// <summary>Машина перезаряжается после печати.</summary>
    [ViewVariables]
    public TimeSpan? ReadyAt;

    [DataField]
    public TimeSpan CultureDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan VaccineDelay = TimeSpan.FromSeconds(20);

    [DataField]
    public string BeakerSlot = "beakerSlot";

    [DataField]
    public EntProtoId BottleProto = "ImperialDiseaseBottle";
}

/// <summary>Симптом «Антикоагулянт»: кровь не сворачивается.</summary>
[RegisterComponent]
public sealed partial class DiseaseBleedingComponent : Component;
