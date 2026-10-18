using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>
/// Продвинутые болезни 1 в 1 с SS13 (advance.dm): характеристики, смешивание и мутации.
/// </summary>
public sealed partial class DiseaseSystem
{
    /// <summary>SSdisease.archive_diseases: справочник всех встреченных болезней по их набору симптомов.</summary>
    private readonly Dictionary<string, DiseaseInstance> _archive = new();

    private void InitializeAdvance()
    {
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ => _archive.Clear());
    }

    /// <summary>Создать пустую продвинутую болезнь.</summary>
    public DiseaseInstance CreateAdvanceDisease(IEnumerable<ProtoId<DiseaseSymptomPrototype>> symptoms, string? name = null)
    {
        var disease = new DiseaseInstance
        {
            Advanced = true,
            Name = name ?? Loc.GetString("disease-name-unknown"),
            Agent = "disease-agent-advance",
            Description = "disease-advance-desc",
            MaxStages = 5,
        };

        foreach (var symptom in symptoms)
        {
            AddSymptom(disease, new SymptomInstance(symptom));
        }

        Refresh(disease, name == null);
        return disease;
    }

    /// <summary>Refresh(): пересчитать характеристики и занести болезнь в справочник.</summary>
    public void Refresh(DiseaseInstance disease, bool newName = false)
    {
        GenerateProperties(disease);
        AssignProperties(disease);

        var id = disease.GetId();
        if (!_archive.ContainsKey(id))
        {
            var archived = disease.Copy();
            archived.Name = newName ? Loc.GetString("disease-name-unknown") : disease.Name;
            _archive[id] = archived;
        }

        disease.Name = _archive[id].Name;
    }

    /// <summary>GenerateProperties(): суммирует вклад симптомов.</summary>
    private void GenerateProperties(DiseaseInstance disease)
    {
        disease.Resistance = 0;
        disease.Stealth = 0;
        disease.StageRate = 0;
        disease.Transmittable = 0;
        var severity = 0;

        foreach (var symptom in disease.Symptoms)
        {
            if (!_proto.TryIndex(symptom.Proto, out var proto))
                continue;

            disease.Resistance += proto.Resistance;
            disease.Stealth += proto.Stealth;
            disease.StageRate += proto.StageSpeed;
            disease.Transmittable += proto.Transmittable;

            if (!symptom.Neutered)
                severity += proto.Severity;
        }

        if (severity > 0)
        {
            severity += (int) MathF.Round(disease.Resistance / 12f);
            severity += (int) MathF.Round(disease.StageRate / 11f);
            severity += (int) MathF.Round(disease.Transmittable / 8f);
            var value = MathF.Round(severity / 2f);
            // Чем меньше симптомов, тем мягче болезнь.
            value *= (float) disease.Symptoms.Count / SymptomLimit;
            severity = (int) MathF.Round(Math.Clamp(value, 1, 7));
        }

        disease.SeverityValue = severity;
    }

    /// <summary>assign_properties(): характеристики превращаются в поведение болезни.</summary>
    private void AssignProperties(DiseaseInstance disease)
    {
        if (disease.Stealth >= disease.SeverityValue && disease.SeverityValue > 0)
            disease.Visibility |= DiseaseVisibility.Scanner;
        else
            disease.Visibility &= ~DiseaseVisibility.Scanner;

        if (disease.Transmittable >= 11)
            SetSpread(disease, DiseaseSpread.Airborne);
        else if (disease.Transmittable >= 7)
            SetSpread(disease, DiseaseSpread.ContactSkin);
        else if (disease.Transmittable >= 3)
            SetSpread(disease, DiseaseSpread.ContactFluids);
        else
            SetSpread(disease, DiseaseSpread.Blood);

        disease.SpreadingModifier = Math.Max(MathF.Ceiling(0.4f * disease.Transmittable), 1);
        disease.CureChance = Math.Clamp(10 * MathF.Pow(0.94f, disease.Resistance), 3.5f, 12);
        disease.StageProbability = Math.Max(0.2f * disease.StageRate, 0) + 1.5f;
        disease.Severity = SeverityFromValue(disease.SeverityValue);
    }

    private static DiseaseSeverity SeverityFromValue(int value)
    {
        return value switch
        {
            <= 0 => DiseaseSeverity.Positive,
            1 => DiseaseSeverity.NonThreat,
            2 => DiseaseSeverity.Minor,
            3 => DiseaseSeverity.Medium,
            4 => DiseaseSeverity.Harmful,
            5 => DiseaseSeverity.Dangerous,
            _ => DiseaseSeverity.Biohazard,
        };
    }

    /// <summary>set_spread(): более заразные способы включают в себя все предыдущие.</summary>
    private void SetSpread(DiseaseInstance disease, DiseaseSpread spread)
    {
        disease.Spread = spread switch
        {
            DiseaseSpread.Blood => DiseaseSpread.Blood,
            DiseaseSpread.ContactFluids => DiseaseSpread.Blood | DiseaseSpread.ContactFluids,
            DiseaseSpread.ContactSkin => DiseaseSpread.Blood | DiseaseSpread.ContactFluids | DiseaseSpread.ContactSkin,
            DiseaseSpread.Airborne => DiseaseSpread.Blood | DiseaseSpread.ContactFluids | DiseaseSpread.ContactSkin | DiseaseSpread.Airborne,
            _ => DiseaseSpread.NonContagious,
        };
    }

    public string GetSpreadText(DiseaseInstance disease)
    {
        if ((disease.Spread & DiseaseSpread.Airborne) != 0)
            return Loc.GetString("disease-spread-airborne");
        if ((disease.Spread & DiseaseSpread.ContactSkin) != 0)
            return Loc.GetString("disease-spread-skin");
        if ((disease.Spread & DiseaseSpread.ContactFluids) != 0)
            return Loc.GetString("disease-spread-fluids");
        if ((disease.Spread & DiseaseSpread.Blood) != 0)
            return Loc.GetString("disease-spread-blood");
        return Loc.GetString("disease-spread-none");
    }

    #region Симптомы

    public bool HasSymptom(DiseaseInstance disease, ProtoId<DiseaseSymptomPrototype> symptom)
    {
        return disease.Symptoms.Any(s => s.Proto == symptom);
    }

    /// <summary>AddSymptom(): сверх лимита случайный симптом вытесняется.</summary>
    public void AddSymptom(DiseaseInstance disease, SymptomInstance symptom)
    {
        if (HasSymptom(disease, symptom.Proto))
            return;

        if (disease.Symptoms.Count >= SymptomLimit)
            disease.Symptoms.Remove(_random.Pick(disease.Symptoms));

        disease.Symptoms.Add(symptom);
    }

    /// <summary>GenerateSymptoms(): случайные симптомы в заданном диапазоне уровней.</summary>
    public List<ProtoId<DiseaseSymptomPrototype>> GenerateSymptoms(DiseaseInstance disease, int levelMin, int levelMax, int amount = 0)
    {
        var possible = new List<ProtoId<DiseaseSymptomPrototype>>();
        foreach (var proto in _proto.EnumeratePrototypes<DiseaseSymptomPrototype>())
        {
            if (!proto.NaturallyOccurring || proto.Level < levelMin || proto.Level > levelMax)
                continue;

            if (!HasSymptom(disease, proto.ID))
                possible.Add(proto.ID);
        }

        var result = new List<ProtoId<DiseaseSymptomPrototype>>();
        if (possible.Count == 0)
            return result;

        var number = amount;
        if (number == 0)
        {
            number = 1;
            while (_random.Prob(0.2f))
            {
                number++;
            }
        }

        for (var i = 0; i < number && possible.Count > 0; i++)
        {
            var pick = _random.PickAndTake(possible);
            result.Add(pick);
        }

        return result;
    }

    /// <summary>Evolve(): добавить случайный симптом.</summary>
    public void Evolve(DiseaseInstance disease, int levelMin, int levelMax, bool ignoreMutable = false)
    {
        if (!disease.Mutable && !ignoreMutable)
            return;

        var generated = GenerateSymptoms(disease, levelMin, levelMax, 1);
        if (generated.Count == 0)
            return;

        AddSymptom(disease, new SymptomInstance(_random.Pick(generated)));
        Refresh(disease, true);
    }

    /// <summary>Devolve(): убрать случайный симптом.</summary>
    public void Devolve(DiseaseInstance disease, bool ignoreMutable = false)
    {
        if (!disease.Mutable && !ignoreMutable || disease.Symptoms.Count <= 1)
            return;

        disease.Symptoms.Remove(_random.Pick(disease.Symptoms));
        Refresh(disease, true);
    }

    /// <summary>Neuter(): симптом остаётся в характеристиках, но перестаёт действовать.</summary>
    public void Neuter(DiseaseInstance disease, bool ignoreMutable = false)
    {
        if (!disease.Mutable && !ignoreMutable || disease.Symptoms.Count == 0)
            return;

        var symptom = _random.Pick(disease.Symptoms);
        if (!symptom.Neutered)
            symptom.Neutered = true;

        Refresh(disease, true);
    }

    /// <summary>Mix(): перенести симптомы одной болезни в другую.</summary>
    public void Mix(DiseaseInstance target, DiseaseInstance source)
    {
        if (target.GetId() == source.GetId())
            return;

        var symptoms = source.Symptoms.ToList();
        _random.Shuffle(symptoms);
        foreach (var symptom in symptoms)
        {
            AddSymptom(target, symptom.Copy());
        }
    }

    /// <summary>Advance_Mix(): свести список болезней в одну.</summary>
    public DiseaseInstance? MixAll(List<DiseaseInstance> diseases)
    {
        var pool = diseases.Where(d => d.Advanced).Select(d => d.Copy()).ToList();
        if (pool.Count == 0)
            return null;

        if (pool.Count == 1)
            return pool[0];

        var i = 0;
        while (i < 20 && pool.Count > 1)
        {
            i++;
            var first = _random.PickAndTake(pool);
            var second = _random.Pick(pool);
            Mix(second, first);
        }

        var result = _random.Pick(pool);
        Refresh(result, true);
        return result;
    }

    /// <summary>AssignName(): имя сохраняется в справочнике для всех копий болезни.</summary>
    public void AssignName(DiseaseInstance disease, string name)
    {
        var id = disease.GetId();
        if (_archive.TryGetValue(id, out var archived))
            archived.Name = name;

        disease.Name = name;
    }

    public DiseaseInstance? GetArchived(string id)
    {
        return _archive.GetValueOrDefault(id);
    }

    public string GetDiseaseName(string id)
    {
        return _archive.TryGetValue(id, out var disease) ? disease.Name : Loc.GetString("disease-name-unknown");
    }

    /// <summary>Собрать экземпляр болезни из прототипа.</summary>
    public DiseaseInstance CreateFromPrototype(DiseasePrototype proto)
    {
        var disease = new DiseaseInstance
        {
            Proto = proto.ID,
            Name = Loc.GetString(proto.Name),
            Agent = proto.Agent,
            Description = proto.Description,
            MaxStages = proto.MaxStages,
            StageProbability = proto.StageProbability,
            Infectivity = proto.Infectivity,
            CureChance = proto.CureChance,
            SpreadingModifier = proto.SpreadingModifier,
            Severity = proto.Severity,
            Flags = proto.Flags,
            Spread = proto.Spread,
            Visibility = proto.Visibility,
            Cures = new List<ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype>>(proto.Cures),
            NeedsAllCures = proto.NeedsAllCures,
            RequiredOrgan = proto.RequiredOrgan,
            BypassesImmunity = proto.BypassesImmunity,
            ProcessDead = proto.ProcessDead,
        };

        // Болезни с готовым набором симптомов ведут себя как продвинутые, но их нельзя менять.
        if (proto.Symptoms.Count > 0)
        {
            disease.Advanced = true;
            disease.Mutable = false;
            foreach (var symptom in proto.Symptoms)
            {
                disease.Symptoms.Add(new SymptomInstance(symptom));
            }

            GenerateProperties(disease);
        }

        return disease;
    }

    /// <summary>disease/advance/random: случайная болезнь для ящиков и событий.</summary>
    public DiseaseInstance CreateRandomDisease(int maxSymptoms = 0, int maxLevel = 8)
    {
        if (maxSymptoms <= 0)
            maxSymptoms = _random.Next(1, SymptomLimit + 1);

        var possible = _proto.EnumeratePrototypes<DiseaseSymptomPrototype>()
            .Where(p => p.Level > 0 && p.Level <= maxLevel && p.NaturallyOccurring)
            .Select(p => (ProtoId<DiseaseSymptomPrototype>) p.ID)
            .ToList();

        var disease = new DiseaseInstance
        {
            Advanced = true,
            Agent = "disease-agent-advance",
            Description = "disease-advance-desc",
            MaxStages = 5,
        };

        for (var i = 0; i < maxSymptoms && possible.Count > 0; i++)
        {
            AddSymptom(disease, new SymptomInstance(_random.PickAndTake(possible)));
        }

        Refresh(disease);
        disease.Name = Loc.GetString("disease-name-sample", ("number", _random.Next(1, 10001)));
        AssignName(disease, disease.Name);
        return disease;
    }

    #endregion
}
