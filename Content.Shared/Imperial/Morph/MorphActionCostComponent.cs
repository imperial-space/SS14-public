using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Morph;

/// <summary>spell/morph_spell: цена способности в еде (hunger_cost).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MorphActionCostComponent : Component
{
    [DataField]
    public int HungerCost;
}
