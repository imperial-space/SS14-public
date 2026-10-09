using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Молот гордыни: с шансом при ударе переливает химикаты из крови владельца в цель.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NullRodChemicalTransferComponent : Component
{
    [DataField]
    public float Chance = 0.3f;
}
