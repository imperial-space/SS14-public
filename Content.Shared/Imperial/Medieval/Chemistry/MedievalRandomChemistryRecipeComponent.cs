using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Chemistry;

[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalRandomChemistryRecipeComponent : Component
{
    [DataField]
    public Dictionary<string, float> Weights = new();
    public ReagentPrototype Reagent = default!;
}
