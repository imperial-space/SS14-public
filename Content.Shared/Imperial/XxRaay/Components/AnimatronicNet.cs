using System;
using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.XxRaay.Components;

[NetSerializable, Serializable]
public sealed class RequestAnimDataEvent : BoundUserInterfaceMessage
{
}

[NetSerializable, Serializable]
public sealed class SetAnimatronicTargetEvent : BoundUserInterfaceMessage
{
	public NetEntity Animatronic { get; private set; }
	public NetEntity Waypoint { get; private set; }
	public bool Clear { get; private set; }

	public SetAnimatronicTargetEvent()
	{
	}

	public SetAnimatronicTargetEvent(NetEntity animatronic, NetEntity waypoint)
	{
		Animatronic = animatronic;
		Waypoint = waypoint;
	}

	public SetAnimatronicTargetEvent(NetEntity animatronic, bool clear)
	{
		Animatronic = animatronic;
		Clear = clear;
	}
}

[NetSerializable, Serializable]
public sealed class SetAnimatronicObservingEvent : BoundUserInterfaceMessage
{
	public NetEntity Animatronic { get; private set; }

	public SetAnimatronicObservingEvent()
	{
	}

	public SetAnimatronicObservingEvent(NetEntity animatronic)
	{
		Animatronic = animatronic;
	}
}

[NetSerializable, Serializable]
public sealed class AnimDataStateEvent : BoundUserInterfaceState
{
	public IReadOnlyList<AnimDto> Animatronics { get; private set; } = Array.Empty<AnimDto>();
	public IReadOnlyList<WaypointDto> Waypoints { get; private set; } = Array.Empty<WaypointDto>();

	public AnimDataStateEvent()
	{
	}

	public AnimDataStateEvent(IReadOnlyList<AnimDto> animatronics, IReadOnlyList<WaypointDto> waypoints)
	{
		Animatronics = animatronics;
		Waypoints = waypoints;
	}
}

[NetSerializable, Serializable]
public sealed class AnimDto
{
	public NetEntity Entity { get; private set; }
	public string DisplayName { get; private set; } = string.Empty;
	public string? CurrentWaypointId { get; private set; }

	public AnimDto()
	{
	}

	public AnimDto(NetEntity entity, string displayName, string? currentWaypointId)
	{
		Entity = entity;
		DisplayName = displayName;
		CurrentWaypointId = currentWaypointId;
	}
}

[NetSerializable, Serializable]
public sealed class WaypointDto
{
	public NetEntity Entity { get; private set; }
	public string WaypointId { get; private set; } = string.Empty;
	public string DisplayName { get; private set; } = string.Empty;

	public WaypointDto()
	{
	}

	public WaypointDto(NetEntity entity, string waypointId, string displayName)
	{
		Entity = entity;
		WaypointId = waypointId;
		DisplayName = displayName;
	}
}
