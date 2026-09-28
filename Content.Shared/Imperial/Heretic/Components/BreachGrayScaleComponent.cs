using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Temporarily added to a player when they over-examine a reality breach.
/// Client reacts by enabling the greyscale screen overlay.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BreachGrayScaleComponent : Component { }
