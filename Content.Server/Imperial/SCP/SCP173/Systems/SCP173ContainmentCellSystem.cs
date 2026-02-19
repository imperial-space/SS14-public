using Content.Server.Imperial.SCP.SCP173.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Robust.Server.Containers;

namespace Content.Server.Imperial.SCP.SCP173.Systems;

public sealed class SCP173ContainmentCellSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SCP173WatchLockComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var lockComp, out var mobState))
        {
            var contained = false;

            if (mobState.CurrentState != MobState.Dead)
                contained = IsInsideContainmentCell(uid);

            if (lockComp.IsContainedByCell == contained)
                continue;

            lockComp.IsContainedByCell = contained;
            _movement.RefreshMovementSpeedModifiers(uid);
        }
    }

    private bool IsInsideContainmentCell(EntityUid target)
    {
        var current = target;
        while (_container.TryGetContainingContainer(current, out var container))
        {
            if (HasComp<SCP173ContainmentCellComponent>(container.Owner))
                return true;

            current = container.Owner;
        }

        return false;
    }
}