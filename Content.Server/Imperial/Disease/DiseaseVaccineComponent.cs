using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>Вакцина: при попадании в кровь даёт иммунитет к указанным болезням.</summary>
[RegisterComponent]
public sealed partial class DiseaseVaccineComponent : Component
{
    [DataField]
    public List<string> Diseases = new();
}
