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
