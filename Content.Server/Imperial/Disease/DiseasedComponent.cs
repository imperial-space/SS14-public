using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>mob/living/diseases: болезни, которыми болеет существо.</summary>
[RegisterComponent]
public sealed partial class DiseasedComponent : Component
{
    [ViewVariables]
    public List<DiseaseInstance> Diseases = new();
}
