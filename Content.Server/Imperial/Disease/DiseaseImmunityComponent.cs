using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>disease_resistances: приобретённый иммунитет.</summary>
[RegisterComponent]
public sealed partial class DiseaseImmunityComponent : Component
{
    [ViewVariables]
    public HashSet<string> Resistances = new();
}
