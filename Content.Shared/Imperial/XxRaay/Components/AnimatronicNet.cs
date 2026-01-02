using System;
using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.XxRaay.Components;

/// <summary>
/// Network events and DTOs for animatronic controller UI.
/// </summary>
[NetSerializable, Serializable]
public sealed class RequestAnimDataEvent : BoundUserInterfaceMessage
{
}

[NetSerializable, Serializable]
public sealed class SetAnimatronicTargetEvent : BoundUserInterfaceMessage
{
	public NetEntity Animatronic;
	public NetEntity Waypoint;
	public bool Clear;

	public SetAnimatronicTargetEvent(NetEntity animatronic, NetEntity waypoint)
	{
		Animatronic = animatronic;
		Waypoint = waypoint;
		Clear = false;
	}

	public SetAnimatronicTargetEvent(NetEntity animatronic, bool clear)
	{
		Animatronic = animatronic;
		Waypoint = default;
		Clear = clear;
	}
}

[NetSerializable, Serializable]
public sealed class SetAnimatronicObservingEvent : BoundUserInterfaceMessage
{
	public NetEntity Animatronic;

	public SetAnimatronicObservingEvent(NetEntity animatronic)
	{
		Animatronic = animatronic;
	}
}

[NetSerializable, Serializable]
public sealed class AnimDataStateEvent : BoundUserInterfaceState
{
	public List<AnimDto> Animatronics = new();
	public List<WaypointDto> Waypoints = new();

	public AnimDataStateEvent()
	{
	}

	public AnimDataStateEvent(List<AnimDto> anims, List<WaypointDto> waypoints)
	{
		Animatronics = anims;
		Waypoints = waypoints;
	}
}

[NetSerializable, Serializable]
public sealed class AnimDto
{
	public NetEntity Entity;
	public string DisplayName = string.Empty;
	public string? CurrentWaypointId;

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
	public NetEntity Entity;
	public string WaypointId = string.Empty;
	public string DisplayName = string.Empty;

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


