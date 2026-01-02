using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.XxRaay.Components;

/// <summary>
/// Marker component for animatronic waypoints.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class AnimatronicWaypointComponent : Component
{
	[DataField("waypointId")]
	public string WaypointId = string.Empty;

	[DataField("displayName")]
	public string DisplayName = string.Empty;
}


