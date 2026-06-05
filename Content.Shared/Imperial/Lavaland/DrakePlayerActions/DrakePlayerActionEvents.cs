using Content.Shared.Actions;
using Robust.Shared.Map;

namespace Content.Shared.Imperial.Lavaland.DrakePlayerActions;

/// <summary>
/// Raised when a player-controlled drake fires the fire-cone ability.
/// The player aims at a world position; the server spawns three fire rays.
/// </summary>
public sealed partial class DrakeFireConeEvent : WorldTargetActionEvent
{
}

/// <summary>
/// Raised when a player-controlled drake uses the flight/swoop ability.
/// The player aims at a world position; the server teleports the entity there.
/// </summary>
public sealed partial class DrakeFlightEvent : WorldTargetActionEvent
{
}
