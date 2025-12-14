using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Plague;

public sealed partial class GrantPlagueImmunity : EntityEffect
{
    public override void Effect(EntityEffectBaseArgs args)
    {
        var plague = args.EntityManager.System<SharedMedievalPlagueSystem>();

        if (args is EntityEffectReagentArgs reagentArgs)
        {
            if (reagentArgs.Reagent != null)
                plague.GrantPlagueImmunity(args.TargetEntity, reagentArgs.Reagent.ID);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-plague-immunity", ("chance", Probability));
}
