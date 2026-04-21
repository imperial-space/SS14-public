using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.ODM.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Input;
using Content.Shared.Inventory;
using Content.Shared.Kitchen.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.ODM;

public sealed class OdmGearSystem : VirtualController
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string LeftJointId = "odm-left";
    private const string RightJointId = "odm-right";

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ImperialOdmLeftHook, new PointerInputCmdHandler(HandleLeftHookInput, outsidePrediction: false))
            .Bind(ContentKeyFunctions.ImperialOdmRightHook, new PointerInputCmdHandler(HandleRightHookInput, outsidePrediction: false))
            .Register<OdmGearSystem>();

        SubscribeLocalEvent<OdmGearComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OdmGearComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<OdmGearComponent, OdmLeftHookActionEvent>(OnLeftHook);
        SubscribeLocalEvent<OdmGearComponent, OdmRightHookActionEvent>(OnRightHook);
        SubscribeLocalEvent<OdmGearComponent, OdmRetractActionEvent>(OnRetract);
        SubscribeLocalEvent<OdmGearComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<OdmGearComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<OdmGearComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<OdmAnchorComponent, ComponentShutdown>(OnAnchorShutdown);
        SubscribeLocalEvent<OdmUserComponent, StartCollideEvent>(OnUserCollide);

        UpdatesBefore.Add(typeof(SharedJointSystem));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<OdmGearSystem>();
    }

    private bool HandleLeftHookInput(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        return HandleHookInput(session, coords, OdmHookSide.Left);
    }

    private bool HandleRightHookInput(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        return HandleHookInput(session, coords, OdmHookSide.Right);
    }

    private bool HandleHookInput(ICommonSession? session, EntityCoordinates coords, OdmHookSide side)
    {
        if (session?.AttachedEntity is not { Valid: true } user || !TryGetEquippedGear(user, out var gear))
            return false;

        if (!_net.IsClient)
            return true;

        var action = side == OdmHookSide.Left ? gear.Comp.LeftActionEntity : gear.Comp.RightActionEntity;
        if (action == null)
            return true;

        RaisePredictiveEvent(new RequestPerformActionEvent(GetNetEntity(action.Value), GetNetCoordinates(coords)));
        return true;
    }

    private void OnMapInit(Entity<OdmGearComponent> ent, ref MapInitEvent args)
    {
        _actionContainer.EnsureAction(ent.Owner, ref ent.Comp.LeftActionEntity, ent.Comp.LeftAction);
        _actionContainer.EnsureAction(ent.Owner, ref ent.Comp.RightActionEntity, ent.Comp.RightAction);
        _actionContainer.EnsureAction(ent.Owner, ref ent.Comp.RetractActionEntity, ent.Comp.RetractAction);
        Dirty(ent);
    }

    private void OnGetItemActions(Entity<OdmGearComponent> ent, ref GetItemActionsEvent args)
    {
        if ((args.SlotFlags | ent.Comp.RequiredFlags) != ent.Comp.RequiredFlags)
            return;

        args.AddAction(ref ent.Comp.LeftActionEntity, ent.Comp.LeftAction);
        args.AddAction(ref ent.Comp.RightActionEntity, ent.Comp.RightAction);
        args.AddAction(ref ent.Comp.RetractActionEntity, ent.Comp.RetractAction);
    }

    private void OnLeftHook(Entity<OdmGearComponent> ent, ref OdmLeftHookActionEvent args)
    {
        HandleHookAction(ent, args, OdmHookSide.Left);
    }

    private void OnRightHook(Entity<OdmGearComponent> ent, ref OdmRightHookActionEvent args)
    {
        HandleHookAction(ent, args, OdmHookSide.Right);
    }

    private void HandleHookAction(Entity<OdmGearComponent> ent, WorldTargetActionEvent args, OdmHookSide side)
    {
        if (args.Handled || _net.IsClient)
            return;

        args.Handled = true;

        if (!TryGetUser(ent.Owner, out var user))
            return;

        if (ent.Comp.CurrentGas <= 0f)
        {
            if (ent.Comp.EmptySound != null)
                _audio.PlayPvs(ent.Comp.EmptySound, ent.Owner);

            _popup.PopupEntity(Loc.GetString("odm-no-gas"), user, user);
            return;
        }

        if (!HasRequiredBlades(user))
        {
            _popup.PopupEntity(Loc.GetString("odm-need-blades"), user, user);
            return;
        }

        if (!_interaction.InRangeUnobstructed(user, args.Target, ent.Comp.HookRange, popup: true))
            return;

        AttachAnchor(ent, user, side, args.Target);
    }

    private void OnRetract(Entity<OdmGearComponent> ent, ref OdmRetractActionEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        args.Handled = true;
        RetractAll(ent);
    }

    private void OnParentChanged(Entity<OdmGearComponent> ent, ref EntParentChangedMessage args)
    {
        if (_net.IsClient)
            return;

        if (!TryGetUser(ent.Owner, out _))
            RetractAll(ent);
    }

    private void OnDropped(Entity<OdmGearComponent> ent, ref DroppedEvent args)
    {
        if (_net.IsClient)
            return;

        RetractAll(ent);
    }

    private void OnExamined(Entity<OdmGearComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var ratio = ent.Comp.MaxGas <= 0f ? 0f : ent.Comp.CurrentGas / ent.Comp.MaxGas;
        args.PushMarkup(Loc.GetString("odm-examine-gas", ("percent", (int) MathF.Round(ratio * 100f))));
    }

    private void OnAnchorShutdown(Entity<OdmAnchorComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<OdmGearComponent>(ent.Comp.Gear, out var gear))
            return;

        switch (ent.Comp.Side)
        {
            case OdmHookSide.Left:
                gear.LeftAnchor = null;
                break;
            case OdmHookSide.Right:
                gear.RightAnchor = null;
                break;
        }

        if (!HasAnyAnchor(gear) && TryGetUser(gear.Owner, out var user))
            ClearUserState(user);

        Dirty(ent.Comp.Gear, gear);
    }

    private void OnUserCollide(Entity<OdmUserComponent> ent, ref StartCollideEvent args)
    {
        if (_net.IsClient || !TryComp<PhysicsComponent>(ent.Owner, out var body) || !TryComp<OdmGearComponent>(ent.Comp.Gear, out var gear))
            return;

        if (!args.OurFixture.Hard || !args.OtherFixture.Hard)
            return;

        var speed = body.LinearVelocity.Length();
        if (speed < gear.ImpactMinimumSpeed)
            return;

        if (ent.Comp.LastImpact != null && (_timing.CurTime - ent.Comp.LastImpact.Value).TotalSeconds < gear.ImpactCooldown)
            return;

        ent.Comp.LastImpact = _timing.CurTime;
        var scale = gear.ImpactDamageFactor * speed / gear.ImpactMinimumSpeed;
        var damage = new DamageSpecifier();
        damage.DamageDict[gear.DamageType] = 4f * scale;
        _damageable.TryChangeDamage(ent.Owner, damage);
        _stun.TryUpdateStunDuration(ent.Owner, TimeSpan.FromSeconds(gear.ImpactStunSeconds * MathF.Min(scale, 1.5f)));

        if (gear.ImpactSound != null)
            _audio.PlayPvs(gear.ImpactSound, ent.Owner);
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<OdmGearComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);

            if (!TryGetUser(uid, out var user))
            {
                RetractAll(ent);
                continue;
            }

            CleanupMissingAnchors(ent);

            if (!HasAnyAnchor(comp))
            {
                ClearUserState(user);
                continue;
            }

            EnsureComp<OdmUserComponent>(user, out var odmUser);
            odmUser.Gear = uid;

            var usedGas = false;
            usedGas |= UpdateAnchor(ent, user, comp.LeftAnchor, OdmHookSide.Left, LeftJointId, frameTime);
            usedGas |= UpdateAnchor(ent, user, comp.RightAnchor, OdmHookSide.Right, RightJointId, frameTime);

            ConsumeGas(ent, usedGas ? comp.GasUsage : comp.GasUsageIdle, user);
        }
    }

    private bool UpdateAnchor(Entity<OdmGearComponent> gear, EntityUid user, EntityUid? anchorUid, OdmHookSide side, string jointId, float frameTime)
    {
        if (anchorUid == null || !TryComp<OdmAnchorComponent>(anchorUid, out var anchorComp))
            return false;

        if (!TryComp<JointComponent>(user, out var userJoint) ||
            !userJoint.GetJoints.TryGetValue(jointId, out var joint) ||
            joint is not DistanceJoint)
        {
            DetachAnchor(gear, side);
            return false;
        }

        var distance = (DistanceJoint) joint;

        var userPos = _transform.GetWorldPosition(user);
        var anchorPos = _transform.GetWorldPosition(anchorUid.Value);
        var direction = anchorPos - userPos;
        var currentLength = direction.Length();

        if (currentLength <= 0.001f)
            return false;

        anchorComp.DesiredLength = MathF.Max(gear.Comp.MinRopeLength, anchorComp.DesiredLength - gear.Comp.ReelSpeed * frameTime);
        var targetLength = MathF.Max(anchorComp.DesiredLength, gear.Comp.MinRopeLength);
        distance.MinLength = gear.Comp.MinRopeLength;
        distance.MaxLength = MathF.Max(targetLength + gear.Comp.RopeSlack, gear.Comp.MinRopeLength + gear.Comp.RopeSlack);
        distance.Length = MathF.Max(targetLength, currentLength - gear.Comp.RopeSlack);
        distance.Stiffness = gear.Comp.RopeStiffness;
        distance.Breakpoint = gear.Comp.RopeBreakPoint;

        if (currentLength >= distance.MaxLength - gear.Comp.RopeSlack &&
            TryComp<PhysicsComponent>(user, out var body))
        {
            var massFactor = MathF.Min(body.InvMass * 85f, 1f);
            _physics.ApplyLinearImpulse(user, Vector2.Normalize(direction) * gear.Comp.PullForce * massFactor * frameTime, body: body);
            Dirty(user, userJoint);
            return true;
        }

        Dirty(user, userJoint);
        return true;
    }

    private void AttachAnchor(Entity<OdmGearComponent> gear, EntityUid user, OdmHookSide side, EntityCoordinates target)
    {
        DetachAnchor(gear, side);

        var anchorUid = Spawn("OdmHookAnchor", target);
        var anchor = EnsureComp<OdmAnchorComponent>(anchorUid);
        anchor.Gear = gear.Owner;
        anchor.Side = side;

        var anchorPos = _transform.GetWorldPosition(anchorUid);
        var userPos = _transform.GetWorldPosition(user);
        anchor.DesiredLength = MathF.Max(gear.Comp.MinRopeLength, (anchorPos - userPos).Length() - gear.Comp.RopeSlack);

        var visuals = EnsureComp<JointVisualsComponent>(anchorUid);
        visuals.Sprite = gear.Comp.RopeSprite;
        visuals.Target = user;

        var joint = _joints.CreateDistanceJoint(anchorUid, user, id: side == OdmHookSide.Left ? LeftJointId : RightJointId);
        joint.MinLength = gear.Comp.MinRopeLength;
        joint.MaxLength = anchor.DesiredLength + gear.Comp.RopeSlack;
        joint.Length = anchor.DesiredLength;
        joint.Stiffness = gear.Comp.RopeStiffness;
        joint.Breakpoint = gear.Comp.RopeBreakPoint;

        if (side == OdmHookSide.Left)
            gear.Comp.LeftAnchor = anchorUid;
        else
            gear.Comp.RightAnchor = anchorUid;

        EnsureComp<OdmUserComponent>(user).Gear = gear.Owner;

        if (gear.Comp.FireSound != null)
            _audio.PlayPvs(gear.Comp.FireSound, gear.Owner);

        Dirty(gear);
    }

    private void DetachAnchor(Entity<OdmGearComponent> gear, OdmHookSide side)
    {
        var anchorUid = side == OdmHookSide.Left ? gear.Comp.LeftAnchor : gear.Comp.RightAnchor;
        if (anchorUid != null && EntityManager.EntityExists(anchorUid.Value))
            QueueDel(anchorUid.Value);

        if (side == OdmHookSide.Left)
            gear.Comp.LeftAnchor = null;
        else
            gear.Comp.RightAnchor = null;

        if (!TryGetUser(gear.Owner, out var user))
            return;

        if (!HasAnyAnchor(gear.Comp))
            ClearUserState(user);

        Dirty(gear);
    }

    private void RetractAll(Entity<OdmGearComponent> gear)
    {
        if (gear.Comp.LeftAnchor != null)
            DetachAnchor(gear, OdmHookSide.Left);

        if (gear.Comp.RightAnchor != null)
            DetachAnchor(gear, OdmHookSide.Right);

        if (gear.Comp.RetractSound != null)
            _audio.PlayPvs(gear.Comp.RetractSound, gear.Owner);
    }

    private void CleanupMissingAnchors(Entity<OdmGearComponent> gear)
    {
        if (gear.Comp.LeftAnchor != null && !EntityManager.EntityExists(gear.Comp.LeftAnchor.Value))
            gear.Comp.LeftAnchor = null;

        if (gear.Comp.RightAnchor != null && !EntityManager.EntityExists(gear.Comp.RightAnchor.Value))
            gear.Comp.RightAnchor = null;
    }

    private void ConsumeGas(Entity<OdmGearComponent> gear, float amount, EntityUid user)
    {
        if (amount <= 0f)
            return;

        gear.Comp.CurrentGas = MathF.Max(0f, gear.Comp.CurrentGas - amount);
        Dirty(gear);

        if (gear.Comp.CurrentGas > 0f)
            return;

        RetractAll(gear);

        if (gear.Comp.EmptySound != null)
            _audio.PlayPvs(gear.Comp.EmptySound, gear.Owner);

        _popup.PopupEntity(Loc.GetString("odm-no-gas"), user, user);
    }

    private bool TryGetUser(EntityUid gearUid, out EntityUid user)
    {
        user = EntityUid.Invalid;
        if (!_container.TryGetContainingContainer((gearUid, null, null), out var container))
            return false;

        user = container.Owner;
        return true;
    }

    private bool TryGetEquippedGear(EntityUid user, out Entity<OdmGearComponent> gear)
    {
        gear = default;

        if (!_inventory.TryGetSlotEntity(user, "back", out var gearUid))
            return false;

        if (!TryComp<OdmGearComponent>(gearUid.Value, out var comp))
            return false;

        gear = (gearUid.Value, comp);
        return true;
    }

    private bool HasRequiredBlades(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        var sharpCount = 0;
        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (HasComp<SharpComponent>(held))
                sharpCount++;

            if (sharpCount >= 2)
                return true;
        }

        return false;
    }

    private static bool HasAnyAnchor(OdmGearComponent component)
    {
        return component.LeftAnchor != null || component.RightAnchor != null;
    }

    private void ClearUserState(EntityUid user)
    {
        RemCompDeferred<OdmUserComponent>(user);
    }
}