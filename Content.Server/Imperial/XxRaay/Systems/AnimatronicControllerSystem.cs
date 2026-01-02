using System.Collections.Generic;
using Content.Shared.Imperial.XxRaay.Components;
using Content.Shared.Imperial.XxRaay.Components.Events;
using Content.Shared.UserInterface;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Server.NPC.Systems;
using Content.Server.Administration.UI;
using Content.Server.EUI;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Server.Player;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.XxRaay.Systems;

/// <summary>
/// Handles controller interactions and serves animatronic/waypoint lists to the client.
/// </summary>
public sealed class AnimatronicControllerSystem : EntitySystem
{
	[Dependency] private readonly UserInterfaceSystem _ui = default!;
	[Dependency] private readonly NPCSteeringSystem _steering = default!;
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

	private void OnAnimatronicReachedTarget(AnimatronicReachedTargetEvent ev)
	{
		UpdateAllOpenUis();
	}

	private void OnBuiOpened(Entity<AnimatronicControllerComponent> ent, ref BoundUIOpenedEvent args)
	{
		UpdateUi(ent.Owner, args.Actor);
	}

	private void OnRequestAnimData(Entity<AnimatronicControllerComponent> ent, ref RequestAnimDataEvent args)
	{
		UpdateUi(ent.Owner, args.Actor);
	}

	private void OnSetTarget(Entity<AnimatronicControllerComponent> ent, ref SetAnimatronicTargetEvent args)
	{
		var animUid = GetEntity(args.Animatronic);
		if (!TryComp<AnimatronicComponent>(animUid, out var anim))
		{
			UpdateUi(ent.Owner, args.Actor);
			return;
		}

		if (args.Clear)
		{
			anim.TargetWaypoint = null;
			Dirty(animUid, anim);
			_steering.Unregister(animUid);
		}
		else
		{
			var wpUid = GetEntity(args.Waypoint);
			if (Exists(wpUid) && HasComp<AnimatronicWaypointComponent>(wpUid))
			{
				anim.TargetWaypoint = wpUid;
				Dirty(animUid, anim);

				EnsureComp<InputMoverComponent>(animUid);
				EnsureComp<ActiveNPCComponent>(animUid);

				if (TryComp<TransformComponent>(wpUid, out var targetXform))
					_steering.Register(animUid, targetXform.Coordinates);
			}
		}
		
		UpdateUi(ent.Owner, args.Actor);
	}

	private void OnSetObserving(Entity<AnimatronicControllerComponent> ent, ref SetAnimatronicObservingEvent args)
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

	private void UpdateUi(EntityUid controller, EntityUid user)
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

		var state = new AnimDataStateEvent(anims, waypoints);
		_ui.SetUiState(controller, AnimatronicControllerUiKey.Key, state);
	}

	public void UpdateAllOpenUis()
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

		var state = new AnimDataStateEvent(anims, waypoints);

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


