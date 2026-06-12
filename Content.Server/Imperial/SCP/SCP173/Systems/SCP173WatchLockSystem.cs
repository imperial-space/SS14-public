using System.Numerics;
using Content.Server.Imperial.SCP.SCP173.Components;
using Content.Server.Imperial.SCP.SCPBlink.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.SCP.SCP173.Systems;

public sealed class SCP173WatchLockSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SCP173WatchLockComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<SCP173WatchLockComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMove);
    }

    private void OnAttackAttempt(Entity<SCP173WatchLockComponent> ent, ref AttackAttemptEvent args)
    {
        if (ent.Comp.IsContainedByCell)
        {
            args.Cancel();
            return;
        }

        if (!ent.Comp.IsLocked)
            return;

        if (IsDarkForWatchLock(ent, ent.Comp))
            return;

        args.Cancel();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SCP173WatchLockComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
            {
                if (comp.IsLocked)
                    SetLocked((uid, comp), false);
                continue;
            }

            var watched = IsBeingWatched(uid, comp);
            if (watched == comp.IsLocked)
                continue;

            SetLocked((uid, comp), watched);
        }
    }

    private void SetLocked(Entity<SCP173WatchLockComponent> ent, bool locked)
    {
        ent.Comp.IsLocked = locked;
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRefreshMove(Entity<SCP173WatchLockComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.IsContainedByCell)
        {
            args.ModifySpeed(ent.Comp.FrozenWalkModifier, ent.Comp.FrozenSprintModifier);
            return;
        }

        if (!ent.Comp.IsLocked)
            return;

        if (IsDarkForWatchLock(ent, ent.Comp))
            return;

        args.ModifySpeed(ent.Comp.FrozenWalkModifier, ent.Comp.FrozenSprintModifier);
    }

    private bool IsDarkForWatchLock(EntityUid uid, SCP173WatchLockComponent comp)
    {
        return comp.RequireLightToObserve && !IsTargetLit(uid, comp);
    }

    private bool IsBeingWatched(EntityUid target, SCP173WatchLockComponent comp)
    {
        if (comp.RequireLightToObserve && !IsTargetLit(target, comp))
            return false;

        var targetPosition = _transform.GetWorldPosition(target);
        var targetMap = Transform(target).MapID;

        foreach (var observer in _lookup.GetEntitiesInRange(target, comp.ObserveRadius, LookupFlags.Dynamic))
        {
            if (observer == target)
                continue;

            if (TryComp<SCPBlinkableComponent>(observer, out var blink) && blink.IsBlinking)
                continue;

            if (!TryComp<MobStateComponent>(observer, out var mobState) || mobState.CurrentState != MobState.Alive)
                continue;

            if (!TryComp<TransformComponent>(observer, out var observerTransform))
                continue;

            if (observerTransform.MapID != targetMap)
                continue;

            if (comp.RequireUnobstructed && !_interaction.InRangeUnobstructed(observer, target, comp.ObserveRadius + 0.1f))
                continue;

            var observerPosition = _transform.GetWorldPosition(observer);
            var toTarget = targetPosition - observerPosition;
            if (toTarget.LengthSquared() <= 0.001f)
                continue;

            var lookDirection = _transform.GetWorldRotation(observerTransform).ToWorldVec();
            var dot = Vector2.Dot(Vector2.Normalize(lookDirection), Vector2.Normalize(toTarget));
            if (dot >= comp.MinLookDot)
                return true;
        }

        return false;
    }

    private bool IsTargetLit(EntityUid target, SCP173WatchLockComponent comp)
    {
        if (TryComp<PointLightComponent>(target, out var selfLight) && selfLight.Enabled && selfLight.Radius > 0f)
            return true;

        var targetPosition = _transform.GetWorldPosition(target);
        var targetMap = Transform(target).MapID;

        foreach (var ent in _lookup.GetEntitiesInRange(target, comp.LightLookupRadius, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (!TryComp<PointLightComponent>(ent, out var light) || !light.Enabled)
                continue;

            if (!TryComp<TransformComponent>(ent, out var lightTransform) || lightTransform.MapID != targetMap)
                continue;

            var distance = (_transform.GetWorldPosition(ent) - targetPosition).Length();
            if (distance <= light.Radius + comp.LightRadiusPadding)
                return true;
        }

        return false;
    }
}
