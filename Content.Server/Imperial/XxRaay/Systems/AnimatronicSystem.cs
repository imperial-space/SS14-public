using Content.Shared.Imperial.XxRaay.Components;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Components;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Imperial.XxRaay.Systems;

/// <summary>
/// Основная система-координатор для аниматроников.
/// </summary>
public sealed class AnimatronicSystem : EntitySystem
{
	[Dependency] private readonly AnimatronicTargetSystem _targetSystem = default!;
	[Dependency] private readonly AnimatronicPathfindingSystem _pathfindingSystem = default!;
	[Dependency] private readonly NPCSteeringSystem _steering = default!;

	public override void Initialize()
	{
		base.Initialize();
		SubscribeNetworkEvent<SetAnimatronicTargetEvent>(OnSetTarget);
	}

	private void OnSetTarget(SetAnimatronicTargetEvent ev, EntitySessionEventArgs args)
	{
		var animUid = GetEntity(ev.Animatronic);
		if (!TryComp<AnimatronicComponent>(animUid, out var anim))
			return;

		if (ev.Clear)
		{
			_targetSystem.ClearTarget(animUid);
			return;
		}

		var wpUid = GetEntity(ev.Waypoint);
		if (!_targetSystem.SetTarget(animUid, wpUid))
			return;

		if (TryComp<AnimatronicPathfindingComponent>(animUid, out var pathfinding) &&
		    TryComp<NPCSteeringComponent>(animUid, out var steering) &&
		    steering.Status == SteeringStatus.NoPath)
		{
			_pathfindingSystem.InitializePathRetryIfNeeded(animUid, pathfinding);
		}
	}
}


