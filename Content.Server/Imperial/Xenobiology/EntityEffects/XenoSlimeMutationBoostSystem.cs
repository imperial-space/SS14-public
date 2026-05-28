using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Imperial.Xenobiology.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.EntityEffects;

/// <summary>
/// Применяет зелье мутации ксено-слайма — снижает эффективный тир при следующем размножении,
/// тем самым повышая шанс мутации потомков.
/// </summary>
public sealed partial class XenoSlimeMutationBoostSystem
    : EntityEffectSystem<XenoSlimeComponent, XenoSlimeMutationBoost>
{
    protected override void Effect(Entity<XenoSlimeComponent> entity, ref EntityEffectEvent<XenoSlimeMutationBoost> args)
    {
        var comp = entity.Comp;
        var boost = args.Effect.TierReduction;

        // MutationBoost — поле, которое снижает эффективный тир при размножении
        var next = comp.MutationBoost + boost;
        comp.MutationBoost = (byte) Math.Max(0, Math.Min(3, next));
    }
}
