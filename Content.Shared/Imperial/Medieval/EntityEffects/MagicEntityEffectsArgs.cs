using Content.Shared.Imperial.Medieval.GameTicking.Rules;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects;


public record class MagicEntityEffectsArgs : EntityEffectBaseArgs
{
    public EntityUid Performer;

    public EntityUid Action;


    public MagicEntityEffectsArgs(
        EntityUid targetUid,
        EntityUid performerUid,
        EntityUid actionUid,
        EntityManager entityManager
    ) : base(targetUid, entityManager)
    {
        Performer = performerUid;
        Action = actionUid;
    }
}

public sealed partial class AlcoholDrink : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "In round end greentext";
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs)
            return;

        if (reagentArgs.EntityManager.TryGetComponent<AffectRoundStatsComponent>(args.TargetEntity, out var player))
            player.Alcohol++;

        foreach (var barrier in reagentArgs.EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
        {
            barrier.AlcoholDrink++;
        }
    }
}
