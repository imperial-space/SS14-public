using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

/// <summary>Клетка модульного щита (/obj/structure/emergency_shield/modular).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModularShieldComponent : Component
{
    [ViewVariables]
    public EntityUid? Generator;

    [DataField]
    public float ShieldEmpAmount = 15f;
}
