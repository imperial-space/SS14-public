using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>datum/disease: экземпляр болезни на носителе или в образце.</summary>
public sealed class DiseaseInstance
{
    /// <summary>Прототип готовой болезни; null — собранная вирусологом.</summary>
    public ProtoId<DiseasePrototype>? Proto;

    public string Name = Loc.GetString("disease-name-unknown");
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
