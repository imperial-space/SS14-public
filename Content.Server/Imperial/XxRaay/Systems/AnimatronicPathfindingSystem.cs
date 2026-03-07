using Content.Shared.Imperial.XxRaay.Components;
using Content.Shared.Imperial.XxRaay.Components.Events;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Components;
using Content.Shared.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.XxRaay.Systems;

/// <summary>
/// Система для управления pathfinding и движением аниматроников.
/// </summary>
public sealed class AnimatronicPathfindingSystem : EntitySystem
{
	[Dependency] private readonly IGameTiming _timing = default!;
	[Dependency] private readonly NPCSteeringSystem _steering = default!;
	[Dependency] private readonly AnimatronicTargetSystem _targetSystem = default!;

	public override void Initialize()
	{
		base.Initialize();
	}

	public override void Update(float frameTime)
	{
		base.Update(frameTime);

		var query = EntityQueryEnumerator<AnimatronicComponent, AnimatronicPathfindingComponent, TransformComponent>();
		while (query.MoveNext(out var uid, out var anim, out var pathfinding, out var xform))
		{
			if (anim.TargetWaypoint is not { } target)
				continue;

			if (!TryComp<TransformComponent>(target, out var targetXform))
				continue;

			if (xform.MapID == MapId.Nullspace || xform.MapID != targetXform.MapID)
				continue;

			UpdatePathIfNeeded(uid, pathfinding, targetXform);

			HandlePathAvailability(uid, pathfinding, targetXform);

			if (_targetSystem.HasReachedTarget(uid, xform, targetXform))
			{
				OnReachedTarget(uid, pathfinding);
			}
		}
	}

	private void UpdatePathIfNeeded(EntityUid uid, AnimatronicPathfindingComponent pathfinding, TransformComponent targetXform)
	{
		if (!TryComp<NPCSteeringComponent>(uid, out var steering))
			return;

		var currentTargetCoords = targetXform.Coordinates;
		var coordsChanged = !steering.Coordinates.Equals(currentTargetCoords);
		var shouldUpdate = false;

		if (coordsChanged)
		{
			shouldUpdate = true;
		}
		else if (pathfinding.LastWaypointUpdateTime == null ||
		         (_timing.CurTime - pathfinding.LastWaypointUpdateTime.Value) >= pathfinding.WaypointUpdateInterval)
		{
			shouldUpdate = true;
		}

		if (shouldUpdate)
		{
			steering.Coordinates = currentTargetCoords;
			_steering.Register(uid, currentTargetCoords, steering);
			pathfinding.LastPathRetryTime = null;
			pathfinding.LastWaypointUpdateTime = _timing.CurTime;
			steering.FailedPathCount = 0;
			Dirty(uid, pathfinding);
		}
	}

	private void HandlePathAvailability(EntityUid uid, AnimatronicPathfindingComponent pathfinding, TransformComponent targetXform)
	{
		if (!TryComp<NPCSteeringComponent>(uid, out var steeringComp))
			return;

		var hasPath = steeringComp.CurrentPath.Count > 0;
		var isPathfinding = steeringComp.PathfindToken != null && !steeringComp.PathfindToken.IsCancellationRequested;

		var pathAvailable = hasPath || isPathfinding ||
		                    steeringComp.Status == SteeringStatus.Moving ||
		                    steeringComp.Status == SteeringStatus.InRange;

		var timeSinceLastRegister = pathfinding.LastPathRetryTime.HasValue
			? (_timing.CurTime - pathfinding.LastPathRetryTime.Value)
			: TimeSpan.MaxValue;
		var justRegistered = timeSinceLastRegister < AnimatronicConstants.PathRegistrationWaitTime;

		if (pathAvailable && (pathfinding.LastPathRetryTime != null || steeringComp.FailedPathCount > 0))
		{
			pathfinding.LastPathRetryTime = null;
			steeringComp.FailedPathCount = 0;
			_steering.Register(uid, targetXform.Coordinates, steeringComp);
			Dirty(uid, pathfinding);
		}
		else if (!pathAvailable && steeringComp.Status == SteeringStatus.NoPath)
		{
			if (justRegistered && timeSinceLastRegister < AnimatronicConstants.MinPathStatusCheckTime)
			{
				return;
			}

			if (pathfinding.LastPathRetryTime == null)
			{
				pathfinding.LastPathRetryTime = _timing.CurTime;
				Dirty(uid, pathfinding);
			}

			var timeSinceRetry = _timing.CurTime - pathfinding.LastPathRetryTime.Value;
			if (timeSinceRetry >= pathfinding.PathRetryInterval)
			{
				pathfinding.LastPathRetryTime = _timing.CurTime;
				steeringComp.FailedPathCount = 0;
				_steering.Register(uid, targetXform.Coordinates);
				Dirty(uid, pathfinding);
			}
		}
	}

	private void OnReachedTarget(EntityUid uid, AnimatronicPathfindingComponent pathfinding)
	{
		_steering.Unregister(uid);
		_targetSystem.ClearTarget(uid);

		pathfinding.LastPathRetryTime = null;
		pathfinding.LastWaypointUpdateTime = null;
		Dirty(uid, pathfinding);

		RaiseLocalEvent(new AnimatronicReachedTargetEvent(uid));
	}

	public void InitializePathRetryIfNeeded(EntityUid uid, AnimatronicPathfindingComponent? pathfinding = null)
	{
		if (!Resolve(uid, ref pathfinding))
			return;

		if (!TryComp<NPCSteeringComponent>(uid, out var steering) ||
		    steering.Status != SteeringStatus.NoPath)
			return;

		pathfinding.LastPathRetryTime = _timing.CurTime;
		Dirty(uid, pathfinding);
	}
}

