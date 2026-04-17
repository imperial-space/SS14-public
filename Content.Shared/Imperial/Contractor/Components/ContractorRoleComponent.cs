using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Contractor.Components;

/// <summary>
/// Added to mind role entities to tag that they are a contractor.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ContractorRoleComponent : Component;