using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Plague;

public sealed partial class ProgressPlague : EntityEffect
{
    [DataField(required: true)]
    public float ProgressAmount;

    public override void Effect(EntityEffectBaseArgs args)
    {
        // upstream need to fix
        var plague = args.EntityManager.System<SharedMedievalPlagueSystem>();

        var amount = ProgressAmount;
        if (args is EntityEffectReagentArgs reagentArgs)
        {
            if (reagentArgs.Reagent != null)
                plague.TryProgressInfection(args.TargetEntity, amount *= (float)reagentArgs.Quantity, reagentArgs.Reagent.ID);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-progress-plague", ("chance", Probability), ("progress", ProgressAmount));
}
