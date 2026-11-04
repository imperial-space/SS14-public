using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Quirks;

/// <summary>Alcohol Tolerance: пьянеет медленнее.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialQuirkAlcoholToleranceComponent : Component
{
    [DataField]
    public float Multiplier = 0.5f;
}
