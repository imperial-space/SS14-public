using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Imperial.XxRaay.Components;

[RegisterComponent]
public sealed partial class AnimatronicRuntimeComponent : Component
{
	public readonly Queue<EntityCoordinates> Waypoints = new();
}


