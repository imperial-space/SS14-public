using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Навык рыбалки (/datum/skill/fishing). Висит на сущности разума.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFishingSkillComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Experience;
}
