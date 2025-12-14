using Content.Shared.EntityEffects;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.EffectConditions;


/// <summary>
/// Checks target state
/// </summary>
public sealed partial class MagicTargetMobStateCondition : EntityEffectCondition
{
    [DataField(required: true)]
    public MobState AllowedState = MobState.Alive;

    public override bool Condition(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent<MobStateComponent>(args.TargetEntity, out var mobStateComponent)) return false;

        return AllowedState == mobStateComponent.CurrentState;
    }

    public override string GuidebookExplanation(IPrototypeManager prototype) => "";
}
