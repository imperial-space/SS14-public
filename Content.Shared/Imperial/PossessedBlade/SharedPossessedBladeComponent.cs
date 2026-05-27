namespace Content.Shared.Imperial.PossessedBlade;

/// <summary>
/// Marks this entity as a possessed blade — a melee weapon that can be inhabited by a ghost.
/// The ghost can speak through the blade but cannot move it.
/// Use-in-hand triggers the ghost role search (via ToggleableGhostRoleComponent).
/// </summary>
[RegisterComponent]
public sealed partial class PossessedBladeComponent : Component
{
}
