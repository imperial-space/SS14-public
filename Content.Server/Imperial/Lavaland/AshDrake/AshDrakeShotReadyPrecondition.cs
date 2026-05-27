using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC;

namespace Content.Server.Imperial.Lavaland.AshDrake;

public sealed partial class AshDrakeShotReadyPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent(owner, out AshDrakeComponent? component))
            return false;

        return component.MeleeHitsSinceLastShot >= component.NextShotAtMeleeHits;
    }
}
