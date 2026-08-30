using Content.Shared.Chat;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Physics;

namespace Content.Shared.Imperial.Aquila.Backflip;

public sealed class BackflipEmoteSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speedMod = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BackflipEmoteComponent, StartBackflipPhaseEvent>(OnStart);
        SubscribeLocalEvent<BackflipEmoteComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnStart(EntityUid uid, BackflipEmoteComponent comp, StartBackflipPhaseEvent args)
    {
        if (TryComp<FixturesComponent>(uid, out var fixtures) && TryComp<PhysicsComponent>(uid, out var physics))
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                if ((fixture.CollisionMask & (int)CollisionGroup.TableLayer) == 0)
                    continue;

                comp.ClearedFixtures.Add(id);
                _physics.SetCollisionMask(uid, id, fixture, fixture.CollisionMask & ~(int)CollisionGroup.TableLayer, fixtures, physics);
            }
        }

        _speedMod.RefreshMovementSpeedModifiers(uid);
        RaiseNetworkEvent(new PlayBackflipAnimationEvent(GetNetEntity(uid)), uid);
    }

    private void OnRefreshSpeed(EntityUid uid, BackflipEmoteComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (_timing.CurTime < comp.PhaseEndTime)
            args.ModifySpeed(comp.SpeedMultiplier, comp.SpeedMultiplier);
    }

    private void RestoreCollisionMasks(EntityUid uid, BackflipEmoteComponent comp)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures) || !TryComp<PhysicsComponent>(uid, out var physics))
            return;

        foreach (var id in comp.ClearedFixtures)
        {
            if (!fixtures.Fixtures.TryGetValue(id, out var fixture))
                continue;

            _physics.SetCollisionMask(uid, id, fixture, fixture.CollisionMask | (int)CollisionGroup.TableLayer, fixtures, physics);
        }

        comp.ClearedFixtures.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BackflipEmoteComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.PhaseEndTime)
                continue;

            RestoreCollisionMasks(uid, comp);
            _speedMod.RefreshMovementSpeedModifiers(uid);
            RemCompDeferred<BackflipEmoteComponent>(uid);
        }
    }
}

public sealed class StartBackflipPhaseEvent : EntityEventArgs;
