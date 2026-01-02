using Content.Shared.Imperial.XxRaay.Components;
using Content.Shared.Imperial.XxRaay.Components.Events;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Components;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Server.Imperial.XxRaay.Systems;

/// <summary>
/// Handles assigning targets to animatronics and registering them with steering/pathfinding.
/// </summary>
public sealed class AnimatronicSystem : EntitySystem
{
	[Dependency] private readonly IGameTiming _timing = default!;
	[Dependency] private readonly TransformSystem _transform = default!;
	[Dependency] private readonly SharedTransformSystem _sharedTransform = default!;
	[Dependency] private readonly IEntityManager _entMan = default!;
	[Dependency] private readonly NPCSteeringSystem _steering = default!;
	[Dependency] private readonly DamageableSystem _damageable = default!;
	[Dependency] private readonly MobStateSystem _mobState = default!;
	[Dependency] private readonly IPrototypeManager _prototype = default!;

	public override void Initialize()
	{
		base.Initialize();
		SubscribeNetworkEvent<SetAnimatronicTargetEvent>(OnSetTarget);
		SubscribeLocalEvent<AnimatronicComponent, StartCollideEvent>(OnAnimatronicCollide);
		SubscribeLocalEvent<AnimatronicComponent, EndCollideEvent>(OnAnimatronicCollideEnd);
	}

	private void OnSetTarget(SetAnimatronicTargetEvent ev, EntitySessionEventArgs args)
	{
		var animUid = GetEntity(ev.Animatronic);
		if (!TryComp<AnimatronicComponent>(animUid, out var anim))
			return;

		if (ev.Clear)
		{
			Log.Debug($"Animatronic {ToPrettyString(animUid)}: Clearing target waypoint");
			anim.TargetWaypoint = null;
			anim.LastPathRetryTime = null;
			anim.LastWaypointUpdateTime = null;
			Dirty(animUid, anim);
			_steering.Unregister(animUid);
			return;
		}

		var wpUid = GetEntity(ev.Waypoint);
		if (!Exists(wpUid) || !HasComp<AnimatronicWaypointComponent>(wpUid))
		{
			Log.Debug($"Animatronic {ToPrettyString(animUid)}: Invalid waypoint {wpUid}");
			return;
		}

		anim.TargetWaypoint = wpUid;
		anim.LastPathRetryTime = null;
		anim.LastWaypointUpdateTime = null;
		Dirty(animUid, anim);

		EnsureComp<InputMoverComponent>(animUid);
		EnsureComp<ActiveNPCComponent>(animUid);

		if (TryComp<TransformComponent>(wpUid, out var targetXform))
		{
			Log.Debug($"Animatronic {ToPrettyString(animUid)}: Setting target waypoint {ToPrettyString(wpUid)} at {targetXform.Coordinates}");
			_steering.Register(animUid, targetXform.Coordinates);
			
			if (TryComp<NPCSteeringComponent>(animUid, out var steering) && 
			    steering.Status == SteeringStatus.NoPath)
			{
				Log.Debug($"Animatronic {ToPrettyString(animUid)}: Path unavailable immediately, initializing retry timer");
				anim.LastPathRetryTime = _timing.CurTime;
			}
			else if (TryComp<NPCSteeringComponent>(animUid, out var steering2))
			{
				Log.Debug($"Animatronic {ToPrettyString(animUid)}: Path status after registration: {steering2.Status}");
			}
		}
	}

	public override void Update(float frameTime)
	{
		base.Update(frameTime);
		var query = EntityQueryEnumerator<AnimatronicComponent, TransformComponent>();
		while (query.MoveNext(out var uid, out var anim, out var xform))
		{
			if (anim.TargetWaypoint is not { } target)
				continue;
			if (!TryComp<TransformComponent>(target, out var targetXform))
				continue;
			if (xform.MapID == MapId.Nullspace || xform.MapID != targetXform.MapID)
				continue;

			if (TryComp<NPCSteeringComponent>(uid, out var steering))
			{
				var currentTargetCoords = targetXform.Coordinates;
				var coordsChanged = !steering.Coordinates.Equals(currentTargetCoords);
				var shouldUpdate = false;
				
				if (coordsChanged)
				{
					shouldUpdate = true;
				}
				else if (anim.LastWaypointUpdateTime == null || 
				         (_timing.CurTime - anim.LastWaypointUpdateTime.Value) >= anim.WaypointUpdateInterval)
				{
					shouldUpdate = true;
				}
				
				if (shouldUpdate)
				{
					if (coordsChanged)
					{
						Log.Debug($"Animatronic {ToPrettyString(uid)}: Waypoint moved from {steering.Coordinates} to {currentTargetCoords}, updating path");
					}
					else
					{
						Log.Debug($"Animatronic {ToPrettyString(uid)}: Periodic waypoint update at {currentTargetCoords}, refreshing path");
					}
					
					steering.Coordinates = currentTargetCoords;
					_steering.Register(uid, currentTargetCoords, steering);
					anim.LastPathRetryTime = null;
					anim.LastWaypointUpdateTime = _timing.CurTime;
					steering.FailedPathCount = 0;
					Dirty(uid, anim);
				}
			}

			if (TryComp<NPCSteeringComponent>(uid, out var steeringComp))
			{
				var hasPath = steeringComp.CurrentPath.Count > 0;
				var isPathfinding = steeringComp.PathfindToken != null && !steeringComp.PathfindToken.IsCancellationRequested;
				
				var pathAvailable = hasPath || isPathfinding || 
				                    steeringComp.Status == SteeringStatus.Moving || 
				                    steeringComp.Status == SteeringStatus.InRange;
				
				var timeSinceLastRegister = anim.LastPathRetryTime.HasValue 
					? (_timing.CurTime - anim.LastPathRetryTime.Value)
					: TimeSpan.MaxValue;
				var justRegistered = timeSinceLastRegister < TimeSpan.FromSeconds(0.5);
				
				if (pathAvailable && (anim.LastPathRetryTime != null || steeringComp.FailedPathCount > 0))
				{
					Log.Debug($"Animatronic {ToPrettyString(uid)}: Path became available (hasPath: {hasPath}, isPathfinding: {isPathfinding}, status: {steeringComp.Status}), clearing retry timer and refreshing path");
					anim.LastPathRetryTime = null;
					steeringComp.FailedPathCount = 0;
					_steering.Register(uid, targetXform.Coordinates, steeringComp);
					Dirty(uid, anim);
				}
				else if (!pathAvailable && steeringComp.Status == SteeringStatus.NoPath)
				{
					if (justRegistered)
					{
						if (timeSinceLastRegister < TimeSpan.FromSeconds(0.1))
						{
							Log.Debug($"Animatronic {ToPrettyString(uid)}: Just registered path, waiting for status update (hasPath: {hasPath}, isPathfinding: {isPathfinding}, status: {steeringComp.Status})");
						}
					}
					else
					{
						if (anim.LastPathRetryTime == null)
						{
							Log.Debug($"Animatronic {ToPrettyString(uid)}: Path unavailable (hasPath: {hasPath}, isPathfinding: {isPathfinding}, status: {steeringComp.Status}, failedCount: {steeringComp.FailedPathCount}), initializing retry timer");
							anim.LastPathRetryTime = _timing.CurTime;
							Dirty(uid, anim);
						}
						
						var timeSinceRetry = _timing.CurTime - anim.LastPathRetryTime.Value;
						if (timeSinceRetry >= anim.PathRetryInterval)
						{
							Log.Debug($"Animatronic {ToPrettyString(uid)}: Retrying path registration (interval: {anim.PathRetryInterval.TotalSeconds:F1}s, elapsed: {timeSinceRetry.TotalSeconds:F2}s, hasPath: {hasPath}, isPathfinding: {isPathfinding}, status: {steeringComp.Status}, failedCount: {steeringComp.FailedPathCount})");
							anim.LastPathRetryTime = _timing.CurTime;
							steeringComp.FailedPathCount = 0;
							_steering.Register(uid, targetXform.Coordinates);
							Dirty(uid, anim);
						}
					}
				}
			}

			var pos = _transform.GetWorldPosition(xform);
			var goal = _transform.GetWorldPosition(targetXform);
			var distance = Vector2.Distance(pos, goal);
			if (distance <= 0.2f)
			{
				Log.Debug($"Animatronic {ToPrettyString(uid)}: Reached target waypoint {ToPrettyString(target)} (distance: {distance:F2}m)");
				_steering.Unregister(uid);
				anim.TargetWaypoint = null;
				anim.LastPathRetryTime = null;
				anim.LastWaypointUpdateTime = null;
				Dirty(uid, anim);
			}
			RaiseLocalEvent(new AnimatronicReachedTargetEvent());
		}
	}

	private void OnAnimatronicCollide(Entity<AnimatronicComponent> ent, ref StartCollideEvent args)
	{
		var otherUid = args.OtherEntity;

		if (!HasComp<MobStateComponent>(otherUid) || !HasComp<DamageableComponent>(otherUid))
			return;

		if (_mobState.IsDead(otherUid))
			return;

		if (ent.Comp.LastContactDamage.TryGetValue(otherUid, out var lastDamage) &&
		    (_timing.CurTime - lastDamage) < ent.Comp.ContactDamageCooldown)
			return;

		if (!_prototype.TryIndex<DamageGroupPrototype>("Brute", out var bruteGroup))
		{
			Log.Error($"AnimatronicSystem: Failed to find Brute damage group prototype");
			return;
		}

		var damage = new DamageSpecifier(bruteGroup, FixedPoint2.New(200));
		_damageable.TryChangeDamage(otherUid, damage, ignoreResistances: true, origin: ent.Owner);

		ent.Comp.LastContactDamage[otherUid] = _timing.CurTime;

		Log.Debug($"Animatronic {ToPrettyString(ent.Owner)}: Dealt 200 brute damage to {ToPrettyString(otherUid)} on contact");
	}

	private void OnAnimatronicCollideEnd(Entity<AnimatronicComponent> ent, ref EndCollideEvent args)
	{
		var otherUid = args.OtherEntity;
		ent.Comp.LastContactDamage.Remove(otherUid);
	}
}


