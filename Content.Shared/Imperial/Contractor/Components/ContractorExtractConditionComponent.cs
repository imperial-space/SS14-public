using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Contractor.Components;

/// <summary>
/// Objective condition that requires the selected target to be delivered alive to a contractor beacon.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ContractorExtractConditionComponent : Component
{
    [DataField]
    public float Range = 2f;
}