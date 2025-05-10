using Content.Shared.Actions;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Silicons.Borgs;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System;

namespace Content.Shared.Imperial.K9XLunge;

public sealed class K9XLungeSystem : EntitySystem
{
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ThrownItemSystem _thrownItem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedBorgSystem _k9x = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ThrownItemComponent> _thrownItemQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _thrownItemQuery = GetEntityQuery<ThrownItemComponent>();

        SubscribeLocalEvent<K9XLungeComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<K9XLungeComponent, K9XLungeActionEvent>(OnK9XLungeAction);
        SubscribeLocalEvent<K9XLungeComponent, ThrowDoHitEvent>(OnK9XLungeHit);
        SubscribeLocalEvent<K9XLungeComponent, LandEvent>(OnK9XLungeLand);

        SubscribeLocalEvent<K9XLungeStunnedComponent, PullStoppedMessage>(OnK9XLungeStunnedPullStopped);
    }

    private void OnInit(EntityUid uid, K9XLungeComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, component.LeapK9XAction);
    }

    private void OnK9XLungeAction(Entity<K9XLungeComponent> k9x, ref K9XLungeActionEvent args)
    {
        //if (!_k9x.CanAbilityAttackTarget(k9x, args.Target))
            //return;

        if (args.Handled)
            return;

        var attempt = new K9XLungeAttemptEvent();
        RaiseLocalEvent(k9x, ref attempt);

        if (attempt.Cancelled)
            return;

        args.Handled = true;

        //_rmcPulling.TryStopAllPullsFromAndOn(k9x);

        var origin = _transform.GetMapCoordinates(k9x);
        var target = _transform.GetMapCoordinates(args.Target);  // координаты сюда
        var diff = target.Position - origin.Position;
        diff = diff.Normalized() * k9x.Comp.Range;

        k9x.Comp.Charge = diff;
        k9x.Comp.Target = args.Target;      // координаты сюда
        Dirty(k9x);

        //_rmcObstacleSlamming.MakeImmune(k9x);
        //_throwing.TryThrow(k9x, diff, 30, animated: false);

        if (!_physicsQuery.TryGetComponent(k9x, out var physics))
            return;

        foreach (var ent in _physics.GetContactingEntities(k9x.Owner, physics))
        {
            if (ent != args.Target)    // координаты сюда
                continue;

            if (ApplyLungeHitEffects(k9x, ent))
                return;
        }
    }

    private void OnK9XLungeHit(Entity<K9XLungeComponent> k9x, ref ThrowDoHitEvent args)
    {
        if (!_mob.IsAlive(k9x) || HasComp<StunnedComponent>(k9x))
        {
            k9x.Comp.Charge = null;
            k9x.Comp.Target = null;
            return;
        }

        ApplyLungeHitEffects(k9x, args.Target);
    }

    private void OnK9XLungeLand(Entity<K9XLungeComponent> ent, ref LandEvent args)
    {
        if (ent.Comp.Charge == null && ent.Comp.Target == null)
            return;

        var target = ent.Comp.Target;
        ent.Comp.Charge = null;
        ent.Comp.Target = null;
        Dirty(ent);

        if (target == null || _pulling.IsPulling(ent))
            return;

        if (_interaction.InRangeUnobstructed(ent.Owner, target.Value))
            ApplyLungeHitEffects(ent, target.Value);
    }

    private bool ApplyLungeHitEffects(Entity<K9XLungeComponent> k9x, EntityUid targetId)
    {
        if (_mobState.IsDead(targetId))
            return false;

        if (_physicsQuery.TryGetComponent(k9x, out var physics) &&
            _thrownItemQuery.TryGetComponent(k9x, out var thrown))
        {
            _thrownItem.LandComponent(k9x, thrown, physics, true);
            _thrownItem.StopThrow(k9x, thrown);
        }

        if (_timing.IsFirstTimePredicted && k9x.Comp.Charge != null)
            k9x.Comp.Charge = null;

        if (_net.IsServer)
        {
            _stun.TryParalyze(targetId, k9x.Comp.StunTime, true);

            var stunned = EnsureComp<K9XLungeStunnedComponent>(targetId);
            stunned.ExpireAt = _timing.CurTime + k9x.Comp.StunTime;
            Dirty(targetId, stunned);
        }

        if (TryComp(k9x, out MeleeWeaponComponent? melee))
        {
            melee.NextAttack = _timing.CurTime;
            Dirty(k9x, melee);
        }

        _pulling.TryStartPull(k9x, targetId);
        return true;
    }

    private void OnK9XLungeStunnedPullStopped(Entity<K9XLungeStunnedComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        foreach (var effect in ent.Comp.Effects)
        {
            _statusEffects.TryRemoveStatusEffect(ent, effect);
        }
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<K9XLungeStunnedComponent>();
        while (query.MoveNext(out var uid, out var stunned))
        {
            if (time < stunned.ExpireAt)
                continue;

            RemCompDeferred<K9XLungeStunnedComponent>(uid);
        }
    }
}
