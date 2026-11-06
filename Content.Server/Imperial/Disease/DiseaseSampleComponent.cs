using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

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
