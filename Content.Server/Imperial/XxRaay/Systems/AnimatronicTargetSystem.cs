using System.Numerics;
using Content.Shared.Imperial.XxRaay.Components;
using Content.Shared.Imperial.XxRaay.Components.Events;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Imperial.XxRaay.Systems;

/// <summary>
/// Система для управления целями и waypoints аниматроников.
/// </summary>
public sealed class AnimatronicTargetSystem : EntitySystem
{
	[Dependency] private readonly NPCSteeringSystem _steering = default!;
	[Dependency] private readonly SharedTransformSystem TransformSystem = default!;

	public bool SetTarget(EntityUid animatronic, EntityUid? waypoint)
	{
		if (!TryComp<AnimatronicComponent>(animatronic, out var anim))
			return false;

		if (waypoint == null)
		{
			ClearTarget(animatronic);
			return true;
		}

		if (!Exists(waypoint.Value) || !HasComp<AnimatronicWaypointComponent>(waypoint.Value))
			return false;

		anim.TargetWaypoint = waypoint.Value;
		Dirty(animatronic, anim);

		EnsureComp<InputMoverComponent>(animatronic);
		EnsureComp<ActiveNPCComponent>(animatronic);

		if (TryComp<TransformComponent>(waypoint.Value, out var targetXform))
		{
			_steering.Register(animatronic, targetXform.Coordinates);
			return true;
		}

		return false;
	}

	public void ClearTarget(EntityUid animatronic)
	{
		if (!TryComp<AnimatronicComponent>(animatronic, out var anim))
			return;

		anim.TargetWaypoint = null;
		Dirty(animatronic, anim);
		_steering.Unregister(animatronic);

		if (TryComp<AnimatronicPathfindingComponent>(animatronic, out var pathfinding))
		{
			pathfinding.LastPathRetryTime = null;
			pathfinding.LastWaypointUpdateTime = null;
		}
	}

	public bool HasReachedTarget(EntityUid animatronic, TransformComponent animXform, TransformComponent targetXform)
	{
		if (animXform.MapID == MapId.Nullspace || animXform.MapID != targetXform.MapID)
			return false;

		var pos = TransformSystem.GetWorldPosition(animXform);
		var goal = TransformSystem.GetWorldPosition(targetXform);
		var distance = (pos - goal).Length();

		return distance <= AnimatronicConstants.ReachDistance;
	}
}

