using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.EntityEffects;

public sealed partial class MannitolBrainHealEffectSystem : EntityEffectSystem<HereticMoonBrainDamageComponent, MannitolBrainHeal>
{
    protected override void Effect(Entity<HereticMoonBrainDamageComponent> entity, ref EntityEffectEvent<MannitolBrainHeal> args)
    {
        var comp = entity.Comp;
        var heal = args.Effect.HealAmount * args.Scale;

        comp.BrainDamage = Math.Max(0f, comp.BrainDamage - heal);

        if (comp.BrainDamage < HereticMoonBrainDamageComponent.HighThreshold)
            comp.HighThresholdMessageSent = false;
        if (comp.BrainDamage < HereticMoonBrainDamageComponent.LowThreshold)
            comp.LowThresholdMessageSent = false;

        Dirty(entity.Owner, comp);

        if (comp.BrainDamage <= 0f)
            RemCompDeferred<HereticMoonBrainDamageComponent>(entity.Owner);
    }
}
