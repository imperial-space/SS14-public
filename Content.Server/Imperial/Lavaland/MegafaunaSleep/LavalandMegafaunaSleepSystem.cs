using Content.Server.Imperial.Lavaland.MegafaunaSleep;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Lavaland.MegafaunaSleep;

public sealed class LavalandMegafaunaSleepSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandMegafaunaSleepComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LavalandMegafaunaSleepComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnStartup(EntityUid uid, LavalandMegafaunaSleepComponent comp, ComponentStartup args)
    {
        _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefreshSpeed(EntityUid uid, LavalandMegafaunaSleepComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(0f, 0f);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var toWake = new List<EntityUid>();

        var query = EntityQueryEnumerator<LavalandMegafaunaSleepComponent, TransformComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var sleep, out var xform, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
            {
                toWake.Add(uid);
                continue;
            }

            foreach (var session in _playerManager.Sessions)
            {
                if (session.Status != SessionStatus.InGame
                    || session.AttachedEntity is not { Valid: true } playerEnt)
                    continue;

                if (HasComp<GhostComponent>(playerEnt))
                    continue;

                var playerXform = Transform(playerEnt);

                if (playerXform.MapUid != xform.MapUid)
                    continue;

                var dist = (xform.WorldPosition - playerXform.WorldPosition).Length();
                if (dist <= sleep.WakeRadius)
                {
                    toWake.Add(uid);
                    break;
                }
            }
        }

        foreach (var uid in toWake)
        {
            if (!HasComp<LavalandMegafaunaSleepComponent>(uid))
                continue;
            RemComp<LavalandMegafaunaSleepComponent>(uid);
            _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
        }
    }
}
