using System.Collections.Generic;
using Content.Shared.Imperial.XxRaay.Components;
using Content.Shared.Imperial.XxRaay.Components.Events;
using Content.Shared.UserInterface;
using Content.Server.Administration.UI;
using Content.Server.EUI;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Server.Player;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.XxRaay.Systems;

/// <summary>
/// Система для обработки взаимодействий с контроллером аниматроников и предоставления данных UI.
/// </summary>
public sealed class AnimatronicControllerSystem : EntitySystem
{
	[Dependency] private readonly UserInterfaceSystem _ui = default!;
	[Dependency] private readonly AnimatronicTargetSystem _targetSystem = default!;
	[Dependency] private readonly AnimatronicPathfindingSystem _pathfindingSystem = default!;
	[Dependency] private readonly EuiManager _eui = default!;

	public override void Initialize()
	{
		base.Initialize();
		Subs.BuiEvents<AnimatronicControllerComponent>(AnimatronicControllerUiKey.Key, subs =>
		{
			subs.Event<BoundUIOpenedEvent>(OnBuiOpened);
			subs.Event<RequestAnimDataEvent>(OnRequestAnimData);
			subs.Event<SetAnimatronicTargetEvent>(OnSetTarget);
			subs.Event<SetAnimatronicObservingEvent>(OnSetObserving);
		});
		SubscribeLocalEvent<AnimatronicReachedTargetEvent>(OnAnimatronicReachedTarget);
	}

	private void OnAnimatronicReachedTarget(AnimatronicReachedTargetEvent _)
	{
		UpdateAllOpenUis();
	}

	private void OnBuiOpened(Entity<AnimatronicControllerComponent> ent, ref BoundUIOpenedEvent args)
	{
		UpdateUi(ent.Owner);
	}

	private void OnRequestAnimData(Entity<AnimatronicControllerComponent> ent, ref RequestAnimDataEvent args)
	{
		UpdateUi(ent.Owner);
	}

	private void OnSetTarget(Entity<AnimatronicControllerComponent> ent, ref SetAnimatronicTargetEvent args)
	{
		var animUid = GetEntity(args.Animatronic);
		if (!TryComp<AnimatronicComponent>(animUid, out var anim))
		{
			UpdateUi(ent.Owner);
			return;
		}

		EntityUid? wpUid = args.Clear ? null : GetEntity(args.Waypoint);
		_targetSystem.SetTarget(animUid, wpUid);

		if (!args.Clear && TryComp<AnimatronicPathfindingComponent>(animUid, out var pathfinding))
		{
			_pathfindingSystem.InitializePathRetryIfNeeded(animUid, pathfinding);
		}

		UpdateUi(ent.Owner);
	}

	private void OnSetObserving(Entity<AnimatronicControllerComponent> _, ref SetAnimatronicObservingEvent args)
	{
		if (args.Actor is not { Valid: true } player)
			return;

		var animUid = GetEntity(args.Animatronic);
		if (!Exists(animUid) || !HasComp<AnimatronicComponent>(animUid))
			return;

		if (!TryComp<ActorComponent>(player, out var actor))
			return;

		var ui = new AdminCameraEui(animUid);
		_eui.OpenEui(ui, actor.PlayerSession);
	}

	private AnimDataStateEvent BuildAnimDataState()
	{
		var anims = new List<AnimDto>();
		var waypoints = new List<WaypointDto>();

		var animQuery = EntityQueryEnumerator<AnimatronicComponent>();
		while (animQuery.MoveNext(out var entity, out var anim))
		{
			var currentWpId = (anim.TargetWaypoint != null && TryComp<AnimatronicWaypointComponent>(anim.TargetWaypoint.Value, out var wpComp))
				? wpComp.WaypointId
				: null;
			anims.Add(new AnimDto(GetNetEntity(entity), anim.DisplayName, currentWpId));
		}

		var wpQuery = EntityQueryEnumerator<AnimatronicWaypointComponent>();
		while (wpQuery.MoveNext(out var entity, out var wp))
		{
			waypoints.Add(new WaypointDto(GetNetEntity(entity), wp.WaypointId, wp.DisplayName));
		}

		return new AnimDataStateEvent(anims, waypoints);
	}

	private void UpdateUi(EntityUid controller)
	{
		var state = BuildAnimDataState();
		_ui.SetUiState(controller, AnimatronicControllerUiKey.Key, state);
	}

	public void UpdateAllOpenUis()
	{
		var state = BuildAnimDataState();

		var controllerQuery = EntityQueryEnumerator<AnimatronicControllerComponent>();
		while (controllerQuery.MoveNext(out var controller, out var _))
		{
			if (_ui.IsUiOpen(controller, AnimatronicControllerUiKey.Key))
			{
				_ui.SetUiState(controller, AnimatronicControllerUiKey.Key, state);
			}
		}
	}
}


