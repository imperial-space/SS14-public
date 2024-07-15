using Content.Server.Flash;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.ReactionEffects;

[DataDefinition]
public sealed partial class FlashReactionEffect : EntityEffect
{
    [DataField("maxRange", required: true)]
    public float MaxRange = default!;

    [DataField("maxDuration")]
    public float MaxDuration = 3.0f;

    [DataField("slowTo")]
    public float SlowTo = 0.8f;

    [DataField("powerPerUnit")]
    public float PowerPerUnit = 0.25f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-flash",
            ("chance", Probability)
        );

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs) return;

        var flasSystem = args.EntityManager.EntitySysManager.GetEntitySystem<FlashSystem>();

        var range = MathF.Min((float) (reagentArgs.Quantity * PowerPerUnit), MaxRange);
        var duration = MathF.Min((float) (reagentArgs.Quantity * PowerPerUnit), MaxDuration) * 1000f;

        flasSystem.FlashArea(
            args.TargetEntity,
            null,
            range,
            duration,
            SlowTo
        );
    }
}
