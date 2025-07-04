using System.Numerics;
using Content.Server.Imperial.Medieval.Grab.Components;
using Content.Server.Physics.Controllers;
using Content.Shared.Imperial.Medieval.Grab;
using Content.Shared.Imperial.Medieval.Grab.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Systems;

//=========================================================================
// GrabController.cs
//=========================================================================
// Purpose: Applies physical motion to grabbable entities. Handles grab
// movement impulses, rotation alignment, and velocity dampening.
// Author: rhailrake
//=========================================================================

namespace Content.Server.Imperial.Medieval.Grab.Controllers;

public sealed class GrabController : VirtualController
{
    private const float TargetDistance = 0.45f;
    private const float MaxSettleVelocity = 0.05f;
    private const float MaxSettleDistance = 0.05f;
    private const float ShutdownVelocity = 0.2f;
    private const float ShutdownDistance = 0.8f;
    private const float ShutdownMultiplier = 25.0f;
    private const float ImpulseForce = 110f;

    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<GrabberComponent> _grabberQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(MoverController));
        _xformQuery = GetEntityQuery<TransformComponent>();
        _grabberQuery = GetEntityQuery<GrabberComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        SubscribeLocalEvent<ActiveGrabberComponent, MoveEvent>(OnGrabberMove);
        SubscribeLocalEvent<GrabMovingComponent, GrabStoppedEvent>(OnGrabStopped);
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<GrabbableComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var grabbable, out var phys, out var grabbedXform))
        {
            if (grabbable.Grabber is not { Valid: true } grabber)
                continue;

            if (!_grabberQuery.TryGetComponent(grabber, out _))
                continue;

            if (IsEntityDead(uid))
            {
                RemCompDeferred<GrabMovingComponent>(uid);
                continue;
            }

            EnsureComp<GrabMovingComponent>(uid);

            var grabberXform = _xformQuery.GetComponent(grabber);
            var grabberPos = _xform.GetWorldPosition(grabberXform);
            var grabberRot = _xform.GetWorldRotation(grabberXform);

            var offset = grabberRot.ToWorldVec() * TargetDistance;
            var targetPos = grabberPos + offset;

            var currentPos = _xform.GetWorldPosition(grabbedXform);
            var diff = targetPos - currentPos;
            var diffLength = diff.Length();

            if (diffLength < MaxSettleDistance && phys.LinearVelocity.Length() < MaxSettleVelocity)
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: phys);
                continue;
            }

            var accel = diff.Normalized() * ImpulseForce;

            if (diffLength < ShutdownDistance && phys.LinearVelocity.Length() >= ShutdownVelocity)
            {
                var scale = (ShutdownDistance - diffLength) / ShutdownDistance;
                accel -= phys.LinearVelocity * ShutdownMultiplier * scale;
            }

            var impulse = accel * frameTime * phys.Mass;
            _physics.ApplyLinearImpulse(uid, impulse, body: phys);

            _xform.SetWorldRotation(uid, grabberRot);
        }
    }

    private void OnGrabberMove(EntityUid uid, ActiveGrabberComponent component, ref MoveEvent args)
    {
        if (!_grabberQuery.TryComp(uid, out var grabber))
            return;

        if (grabber.GrabbedEntity is not { Valid: true } grabbable)
        {
            RemCompDeferred(uid, component);
            return;
        }

        if (!IsEntityDead(grabbable))
            UpdateGrabbedRotation(uid, grabbable);

        if (args.NewPosition.EntityId == args.OldPosition.EntityId &&
            (args.NewPosition.Position - args.OldPosition.Position).LengthSquared() < 0.0025f)
            return;

        if (TryComp(uid, out PhysicsComponent? physics))
            _physics.WakeBody(uid, body: physics);

        RemCompDeferred<GrabMovingComponent>(grabbable);
    }

    private void UpdateGrabbedRotation(EntityUid grabber, EntityUid grabbed)
    {
        if (!_xformQuery.TryGetComponent(grabber, out var grabberXform))
            return;

        _xform.SetWorldRotation(grabbed, _xform.GetWorldRotation(grabberXform));
    }

    private void OnGrabStopped(EntityUid uid, GrabMovingComponent comp, ref GrabStoppedEvent args)
    {
        RemCompDeferred<GrabMovingComponent>(uid);
    }

    private bool IsEntityDead(EntityUid uid)
    {
        if (!_mobStateQuery.TryGetComponent(uid, out var mobState))
            return false;

        return _mobState.IsDead(uid, mobState);
    }
}
