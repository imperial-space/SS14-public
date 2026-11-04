using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Quirks;

/// <summary>Throwing Arm: брошенное летит сильнее и дальше.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialQuirkThrowingArmComponent : Component
{
    [DataField]
    public float Multiplier = 1.5f;
}
