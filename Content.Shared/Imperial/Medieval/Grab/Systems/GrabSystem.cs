using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Grab.Components;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

//=========================================================================
// GrabSystem.cs
//=========================================================================
// Purpose: Handles grab logic.
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.Grab.Systems;

public sealed class GrabSystem : EntitySystem
{
    #region Dependencies

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _modifierSystem = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly HeldSpeedModifierSystem _clothingMoveSpeed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtual = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;

    #endregion

    #region Initialization

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(SharedPhysicsSystem));
        UpdatesAfter.Add(typeof(PullingSystem));
        UpdatesOutsidePrediction = true;

        #region Grabbable

        SubscribeLocalEvent<GrabbableComponent, MoveInputEvent>(OnGrabbableMoveInput);
        SubscribeLocalEvent<GrabbableComponent, CollisionChangeEvent>(OnGrabbableCollisionChange);
        SubscribeLocalEvent<GrabbableComponent, JointRemovedEvent>(OnJointRemoved);
        SubscribeLocalEvent<GrabbableComponent, EntGotInsertedIntoContainerMessage>(OnGrabbableContainerInsert);
        SubscribeLocalEvent<GrabbableComponent, BeingPulledAttemptEvent>(OnPulledAttempt);
        SubscribeLocalEvent<GrabbableComponent, StartPullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<GrabbableComponent, GrabEscapeDoAfterEvent>(OnGrabEscapeDoAfter);

        // Check balance, and if needed, enable blocker.
        // SubscribeLocalEvent<GrabbableComponent, DropAttemptEvent>(CheckAct);
        // SubscribeLocalEvent<GrabbableComponent, PickupAttemptEvent>(CheckAct);
        // SubscribeLocalEvent<GrabbableComponent, AttackAttemptEvent>(CheckAct);
        // SubscribeLocalEvent<GrabbableComponent, UseAttemptEvent>(CheckAct);
        // SubscribeLocalEvent<GrabbableComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        // SubscribeLocalEvent<GrabbableComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);

        SubscribeLocalEvent<GrabbableComponent, EscapeGrabbingAlertEvent>(OnEscapeGrabAlert);

        #endregion

        #region Grabber

        SubscribeLocalEvent<GrabberComponent, UpdateMobStateEvent>(OnStateChanged, after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<GrabberComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<GrabberComponent, EntGotInsertedIntoContainerMessage>(OnGrabberContainerInsert);
        SubscribeLocalEvent<GrabberComponent, VirtualItemDeletedEvent>(OnVirtualItemDeleted);
        SubscribeLocalEvent<GrabberComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeedGrabber);
        SubscribeLocalEvent<GrabberComponent, GrabDoAfterEvent>(OnGrabDoAfter);
        SubscribeLocalEvent<GrabberComponent, StopGrabbingAlertEvent>(OnStopGrabbingAlert);

        #endregion

        #region Utility

        SubscribeLocalEvent<HandsComponent, GrabStartedEvent>(HandleGrabStarted);
        SubscribeLocalEvent<HandsComponent, GrabStoppedEvent>(HandleGrabStopped);

        SubscribeLocalEvent<GrabbableComponent, StrappedEvent>(OnBuckled);
        SubscribeLocalEvent<GrabbableComponent, BuckledEvent>(OnGotBuckled);

        #endregion
    }

    #endregion

    #region Grabbable Events Handling

    private void OnGrabbableMoveInput(EntityUid uid, GrabbableComponent component, ref MoveInputEvent args)
    {
        if (_net.IsClient)
            return;

        if (component.DoAfterRaised)
            return;

        if (!IsGrabbed(args.Entity))
            return;

        if (component.Grabber is not { Valid: true } grabber)
            return;

        component.DoAfterRaised = true;
        TryStartEscapeDoAfter(uid, grabber);
    }

    private void OnGrabbableCollisionChange(EntityUid uid, GrabbableComponent component, ref CollisionChangeEvent args)
    {
        if (!_timing.ApplyingState && component.GrabJointId != null && !args.CanCollide)
        {
            _joints.RemoveJoint(uid, component.GrabJointId);
        }
    }

    private void OnJointRemoved(EntityUid uid, GrabbableComponent component, JointRemovedEvent args)
    {
        if (component.Grabber != args.OtherEntity ||
            args.Joint.ID != component.GrabJointId ||
            _timing.ApplyingState)
        {
            return;
        }

        if (args.Joint.ID != component.GrabJointId || component.Grabber == null)
            return;

        StopGrabbing(uid, component);
    }

    private void OnGrabbableContainerInsert(EntityUid uid, GrabbableComponent component, ref EntGotInsertedIntoContainerMessage args)
    {
        TryStopGrab(uid, component);
    }

    private void OnPulledAttempt(EntityUid uid, GrabbableComponent component, BeingPulledAttemptEvent args)
    {
        if (IsGrabbed(uid, component))
            args.Cancel();
    }

    private void OnPullAttempt(EntityUid uid, GrabbableComponent component, StartPullAttemptEvent args)
    {
        if (IsGrabbed(uid, component))
            args.Cancel();
    }

    private void OnGrabEscapeDoAfter(EntityUid uid, GrabbableComponent comp, GrabEscapeDoAfterEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Cancelled || args.Handled)
        {
            comp.DoAfterRaised = false;
            return;
        }

        if (!IsGrabbed(uid, comp) || comp.Grabber != GetEntity(args.Grabber))
        {
            comp.DoAfterRaised = false;
            return;
        }

        if (_random.Prob(args.Chance / 100f))
        {
            TryStopGrab(uid, comp);
            args.Handled = true;
            comp.DoAfterRaised = false;
            _popup.PopupEntity(Loc.GetString("grab-escape-success"), uid, uid);
        }
        else
        {
            args.Repeat = true;
        }
    }

    // Check balance, and if needed, enable blocker.
    /*private void CheckAct(EntityUid uid, GrabbableComponent component, CancellableEntityEventArgs args)
    {
        if (IsGrabbed(uid, component))
            args.Cancel();
    }

    private void OnEquipAttempt(EntityUid uid, GrabbableComponent component, IsEquippingAttemptEvent args)
    {
        if (args.Equipee == uid)
            CheckAct(uid, component, args);
    }

    private void OnUnequipAttempt(EntityUid uid, GrabbableComponent component, IsUnequippingAttemptEvent args)
    {
        if (args.Unequipee == uid)
            CheckAct(uid, component, args);
    }*/

    private void OnEscapeGrabAlert(EntityUid uid, GrabbableComponent component, EscapeGrabbingAlertEvent args)
    {
        if (_net.IsClient)
            return;

        if (!IsGrabbed(uid, component) || component.DoAfterRaised)
            return;

        if (component.Grabber is not { Valid: true } grabber)
            return;

        component.DoAfterRaised = true;
        TryStartEscapeDoAfter(uid, grabber);
    }

    #endregion

    #region Grabber Events Handling

    private void OnStateChanged(EntityUid uid, GrabberComponent component, ref UpdateMobStateEvent args)
    {
        if (component.GrabbedEntity == null)
            return;

        if (TryComp<GrabbableComponent>(component.GrabbedEntity, out var comp) && (args.State == MobState.Critical || args.State == MobState.Dead))
        {
            TryStopGrab(component.GrabbedEntity.Value, comp);
        }
    }

    private void OnAfterState(EntityUid uid, GrabberComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (component.GrabbedEntity == null)
            RemComp<ActiveGrabberComponent>(uid);
        else
            EnsureComp<ActiveGrabberComponent>(uid);
    }

    private void OnGrabberContainerInsert(EntityUid uid, GrabberComponent component, ref EntGotInsertedIntoContainerMessage args)
    {
        if (component.GrabbedEntity == null)
            return;

        if (!TryComp(component.GrabbedEntity.Value, out GrabbableComponent? grabbable))
            return;

        TryStopGrab(component.GrabbedEntity.Value, grabbable, uid);
    }

    private void OnVirtualItemDeleted(EntityUid uid, GrabberComponent component, VirtualItemDeletedEvent args)
    {
        if (component.GrabbedEntity == null)
            return;

        if (component.GrabbedEntity != args.BlockingEntity)
            return;

        if (!TryComp<GrabbableComponent>(args.BlockingEntity, out var grabbableComponent))
            return;

        TryStopGrab(args.BlockingEntity, grabbableComponent, uid);
    }

    private void OnRefreshMoveSpeedGrabber(EntityUid uid, GrabberComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (TryComp<HeldSpeedModifierComponent>(component.GrabbedEntity, out var heldMoveSpeed) && component.GrabbedEntity.HasValue)
        {
            var (walkMod, sprintMod) =
                _clothingMoveSpeed.GetHeldMovementSpeedModifiers(component.GrabbedEntity.Value, heldMoveSpeed);
            args.ModifySpeed(walkMod, sprintMod);
            return;
        }

        args.ModifySpeed(component.WalkSpeedModifier, component.SprintSpeedModifier);
    }

    private void OnGrabDoAfter(EntityUid uid, GrabberComponent component, GrabDoAfterEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Cancelled || args.Handled)
            return;

        var target = GetEntity(args.Grabber);

        if (!TryComp<GrabbableComponent>(target, out var grabbable))
            return;

        if (_random.Prob(args.Chance / 100f))
        {
            TryStartGrab(uid, target, component, grabbable);
            args.Handled = true;
        }
        else
        {
            args.Repeat = true;
        }
    }

    private void OnStopGrabbingAlert(EntityUid uid, GrabberComponent component, StopGrabbingAlertEvent args)
    {
        if (_net.IsClient)
            return;

        if (component.GrabbedEntity is not { Valid: true } grabbed)
            return;

        if (!TryComp<GrabbableComponent>(grabbed, out var grabbable))
            return;

        TryStopGrab(grabbed, grabbable, uid);
    }

    #endregion

    #region Utility Events Handling

    private void HandleGrabStarted(EntityUid uid, HandsComponent component, GrabStartedEvent args)
    {
        if (args.GrabberUid != uid)
            return;

        if (!TryComp(args.GrabberUid, out GrabberComponent? _))
            return;

        _virtual.TrySpawnVirtualItemInHand(args.GrabbedUid, uid);
    }

    private void HandleGrabStopped(EntityUid uid, HandsComponent component, GrabStoppedEvent args)
    {
        if (args.GrabberUid != uid)
            return;

        if (!args.GrabberUid.HasValue)
            return;

        foreach (var item in _handsSystem.EnumerateHeld(args.GrabberUid.Value))
        {
            if (!TryComp(item, out VirtualItemComponent? virtualItem)
                || virtualItem.BlockingEntity != args.GrabbedUid)
                continue;

            // Находим руку, в которой находится виртуальный предмет
            if (_handsSystem.IsHolding(args.GrabberUid.Value, item, out var handId))
            {
                _handsSystem.TryDrop(args.GrabberUid.Value, handId);
                break;
            }
        }
    }

    private void OnBuckled(EntityUid uid, GrabbableComponent component, ref StrappedEvent args)
    {
        if (component.Grabber == args.Buckle.Owner && !args.Buckle.Comp.PullStrap)
            StopGrabbing(uid, component);
    }

    private void OnGotBuckled(EntityUid uid, GrabbableComponent component, ref BuckledEvent args)
    {
        StopGrabbing(uid, component);
    }

    #endregion

    #region Public API

    public bool IsGrabbed(EntityUid uid, GrabbableComponent? component = null)
        => Resolve(uid, ref component, false) && component.Grabber != null;

    public bool IsGrabbing(EntityUid uid, GrabberComponent? component = null)
        => Resolve(uid, ref component, false) && component.GrabbedEntity != null;

    public EntityUid? GetGrabber(EntityUid uid, GrabbableComponent? component = null)
        => Resolve(uid, ref component, false) ? component.Grabber : null;

    public EntityUid? GetGrabbing(EntityUid uid, GrabberComponent? component = null)
        => Resolve(uid, ref component, false) ? component.GrabbedEntity : null;

    public bool CanGrab(EntityUid grabber, EntityUid grabbableUid, GrabberComponent? grabberComp = null)
    {
        if (IsGrabbed(grabber))
            return false;

        if (!Resolve(grabber, ref grabberComp, false))
            return false;

        if (!_interaction.InRangeUnobstructed(grabber, grabbableUid, grabberComp.GrabRange))
            return false;

        if (!TryComp<PhysicsComponent>(grabbableUid, out var physics) || physics.BodyType == BodyType.Static)
            return false;

        if (grabber == grabbableUid || !_containerSystem.IsInSameOrNoContainer((grabber, null, null), (grabbableUid, null, null)))
            return false;

        if (!_blocker.CanInteract(grabber, grabbableUid))
            return false;

        if (!_handsSystem.TryGetEmptyHand(grabber, out _))
            return false;

        var beingGrabbed = new BeingGrabbedAttemptEvent(grabber, grabbableUid);
        RaiseLocalEvent(grabbableUid, beingGrabbed, true);
        var startGrabAttempt = new StartGrabAttemptEvent(grabber, grabbableUid);
        RaiseLocalEvent(grabber, startGrabAttempt, true);
        return !beingGrabbed.Cancelled && !startGrabAttempt.Cancelled;
    }

    #endregion

    #region Grab Logic

    public bool ToggleGrab(Entity<GrabbableComponent?> grabbable, EntityUid grabberUid)
    {
        if (TryComp<PullableComponent>(grabbable, out var pullable) && _pullingSystem.IsPulled(grabbable, pullable))
            _pullingSystem.TryStopPull(grabbable, pullable);

        if (TryComp<PullerComponent>(grabberUid, out var puller)
            && puller.Pulling.HasValue
            && TryComp<PullableComponent>(puller.Pulling.Value, out var pullablePulling))
            _pullingSystem.TryStopPull(puller.Pulling.Value, pullablePulling, grabberUid);

        if (!Resolve(grabbable, ref grabbable.Comp, false))
            return false;

        if (grabbable.Comp.Grabber == grabberUid)
            return TryStopGrab(grabbable, grabbable.Comp);

        var attackerScore = GetStrength(grabberUid) * 4 + GetDexterity(grabberUid) * 3;
        var defenderScore = GetStrength(grabbable) * 3 + GetDexterity(grabbable) * 7;
        var chance = 50 + attackerScore - defenderScore;

        var doAfterArgs = new DoAfterArgs(new DoAfterArgs(EntityManager, grabberUid, TimeSpan.FromSeconds(1), new GrabDoAfterEvent(GetNetEntity(grabbable), chance), grabberUid, grabbable, grabberUid))
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            BreakOnWeightlessMove = false,
            DistanceThreshold = Comp<GrabberComponent>(grabberUid).GrabRange
        };

        _doAfter.TryStartDoAfter(doAfterArgs);

        return true;
    }

    public bool TryStartGrab(
        EntityUid grabberUid,
        EntityUid grabbableUid,
        GrabberComponent? grabberComponent = null,
        GrabbableComponent? grabbableComponent = null)
    {
        if (!Resolve(grabberUid, ref grabberComponent, false) ||
            !Resolve(grabbableUid, ref grabbableComponent, false))
            return false;

        if (grabberComponent.GrabbedEntity == grabbableUid)
            return true;

        if (!CanGrab(grabberUid, grabbableUid))
            return false;

        if (!TryComp(grabberUid, out PhysicsComponent? grabberPhysics) ||
            !TryComp(grabbableUid, out PhysicsComponent? grabbablePhysics))
            return false;

        if (TryComp<GrabbableComponent>(grabberComponent.GrabbedEntity, out var oldGrabbable)
            && !TryStopGrab(grabberComponent.GrabbedEntity.Value, oldGrabbable, grabberUid))
            return false;

        if (TryComp<PullableComponent>(grabbableUid, out var pullable) && pullable.BeingPulled)
            _pullingSystem.TryStopPull(grabbableUid, pullable);

        if (grabbableComponent.Grabber != null &&
            !TryStopGrab(grabbableUid, grabbableComponent, grabbableComponent.Grabber))
            return false;

        var grabAttempt = new GrabAttemptEvent(grabberUid, grabbableUid);
        RaiseLocalEvent(grabberUid, grabAttempt);
        RaiseLocalEvent(grabbableUid, grabAttempt);

        if (grabAttempt.Cancelled)
            return false;

        StartGrabbing(grabberUid,
            grabberPhysics,
            grabberComponent,
            grabbableUid,
            grabbablePhysics,
            grabbableComponent);

        return true;
    }

    private void StartGrabbing(EntityUid grabberUid,
        PhysicsComponent grabberPhysics,
        GrabberComponent grabberComp,
        EntityUid grabbableUid,
        PhysicsComponent grabbablePhysics,
        GrabbableComponent grabbableComp)
    {
        if (!CanGrab(grabberUid, grabbableUid))
            return;

        _interaction.DoContactInteraction(grabbableUid, grabberUid);

        grabbableComp.GrabJointId = $"grab-joint-{GetNetEntity(grabbableUid)}";

        EnsureComp<ActiveGrabberComponent>(grabberUid);

        grabberComp.GrabbedEntity = grabbableUid;
        grabbableComp.Grabber = grabberUid;

        if (!_timing.ApplyingState)
        {
            var joint = _joints.CreateDistanceJoint(
                grabbableUid,
                grabberUid,
                grabbablePhysics.LocalCenter,
                grabberPhysics.LocalCenter,
                id: grabbableComp.GrabJointId);

            joint.CollideConnected = false;
            joint.MaxLength = joint.Length + 0.15f;
            joint.MinLength = 0f;
            joint.Stiffness = 0f;
        }

        var message = new GrabStartedEvent(grabberUid, grabbableUid);
        _modifierSystem.RefreshMovementSpeedModifiers(grabberUid);

        _alertsSystem.ShowAlert(grabberUid, "Grabbing");
        _alertsSystem.ShowAlert(grabbableUid, "Grabbed");

        RaiseLocalEvent(grabberUid, message);
        RaiseLocalEvent(grabbableUid, message);

        Dirty(grabberUid, grabberComp);
        Dirty(grabbableUid, grabbableComp);

        _popup.PopupEntity(Loc.GetString("grab-popup-success"), grabbableUid, grabberUid);
        _popup.PopupEntity(Loc.GetString("grabbed-popup"), grabberUid, grabbableUid);
    }

    public bool TryStopGrab(EntityUid grabbableUid, GrabbableComponent grabbableComp, EntityUid? user = null)
    {
        if (grabbableComp.Grabber == null)
            return true;

        if (user != null && !_blocker.CanInteract(user.Value, grabbableUid))
            return false;

        var msg = new StopGrabAttemptEvent(user, grabbableUid);
        RaiseLocalEvent(grabbableUid, msg, true);
        if (msg.Cancelled)
            return false;

        StopGrabbing(grabbableUid, grabbableComp);
        return true;
    }

    private void StopGrabbing(EntityUid grabbableUid, GrabbableComponent grabbableComp)
    {
        if (grabbableComp.Grabber == null)
            return;

        if (!_timing.ApplyingState)
        {
            if (grabbableComp.GrabJointId != null)
            {
                _joints.RemoveJoint(grabbableUid, grabbableComp.GrabJointId);
                grabbableComp.GrabJointId = null;
            }
        }

        var oldGrabber = grabbableComp.Grabber;
        grabbableComp.Grabber = null;
        grabbableComp.GrabJointId = null;
        grabbableComp.DoAfterRaised = false;
        Dirty(grabbableUid, grabbableComp);

        if (oldGrabber != null && TryComp<GrabberComponent>(oldGrabber.Value, out var grabberComponent))
        {
            _alertsSystem.ClearAlert(oldGrabber.Value, "Grabbing");

            grabberComponent.GrabbedEntity = null;
            Dirty(oldGrabber.Value, grabberComponent);
            RemComp<ActiveGrabberComponent>(oldGrabber.Value);

            var message = new GrabStoppedEvent(oldGrabber.Value, grabbableUid);
            _modifierSystem.RefreshMovementSpeedModifiers(oldGrabber.Value);

            RaiseLocalEvent(oldGrabber.Value, message);
            RaiseLocalEvent(grabbableUid, message);
        }

        _alertsSystem.ClearAlert(grabbableUid, "Grabbed");
    }

    #endregion

    #region Helpers

    private void TryStartEscapeDoAfter(EntityUid victim, EntityUid grabber)
    {
        var atkScore = GetStrength(grabber) * 3 + GetDexterity(grabber) * 3;
        var defScore = GetStrength(victim) * 4 + GetDexterity(victim) * 3;
        var chance = 50 - atkScore + defScore;

        var args = new DoAfterArgs(new DoAfterArgs(EntityManager,
            victim,
            TimeSpan.FromSeconds(3),
            new GrabEscapeDoAfterEvent(GetNetEntity(grabber), chance),
            victim,
            victim,
            victim)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            BreakOnWeightlessMove = false,
        });

        _doAfter.TryStartDoAfter(args);
    }


    private int GetDexterity(EntityUid target)
    {
        return GetSkill(target, "Agility").Item2;
    }

    private int GetStrength(EntityUid target)
    {
        return GetSkill(target, "Strength").Item2;
    }

    private (SkillPrototype, int) GetSkill(EntityUid uid, string id)
    {
        var proto = _prototype.Index<SkillPrototype>(id);

        if (!TryComp<SkillsComponent>(uid, out var skillComponent))
            return (proto, 10);

        return (proto, skillComponent.Levels.GetValueOrDefault(id, 10));
    }

    #endregion
}
