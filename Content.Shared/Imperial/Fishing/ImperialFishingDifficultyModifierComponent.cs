using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Предмет облегчает или усложняет рыбалку, пока надет или в руке
/// (/datum/element/adjust_fishing_difficulty).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingDifficultyModifierComponent : Component
{
    [DataField]
    public float Modifier;

    /// <summary>В каких слотах действует. Руки учитываются, если Hands = true.</summary>
    [DataField]
    public SlotFlags Slots = SlotFlags.NONE;

    [DataField]
    public bool Hands;
}
