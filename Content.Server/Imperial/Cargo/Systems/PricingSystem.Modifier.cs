using Content.Server.Imperial.Cargo.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Systems;

public sealed partial class PricingSystem
{
    private double ApplyPrototypePriceModifier(EntityPrototype prototype, double basePrice)
    {
        if (prototype.Components.TryGetValue(_factory.GetComponentName(typeof(PriceModifierComponent)),
                out var modProto))
        {
            var priceModifier = (PriceModifierComponent)modProto.Component;
            return basePrice * priceModifier.Modifier;
        }

        return basePrice;
    }

    private double ApplyPriceModifier(EntityUid uid, double basePrice)
    {
        if (TryComp<PriceModifierComponent>(uid, out var modifier))
        {
            return basePrice * modifier.Modifier;
        }

        return basePrice;
    }
}

// <summary>
// pls help me
// <summary>
