using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Heretic.Components;
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

public sealed partial class MannitolBrainHeal : EntityEffectBase<MannitolBrainHeal>
{
    /// <summary>
    /// Количество единиц урона мозгу, снимаемых за единицу реагента за тик.
    /// </summary>
    [DataField]
    public float HealAmount = 10f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        "Маннитол снижает накопленный урон мозгу от воздействий Пути Луны.";
}
