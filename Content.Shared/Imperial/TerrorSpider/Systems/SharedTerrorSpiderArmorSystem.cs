using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.TerrorSpider.Components;

namespace Content.Shared.Imperial.TerrorSpider.Systems;

public abstract class SharedTerrorSpiderArmorSystem : EntitySystem
{
    // Brute damage types
    private static readonly string[] BruteTypes = { "Blunt", "Slash", "Piercing" };

    // Burn damage types
    private static readonly string[] BurnTypes = { "Heat", "Shock", "Cold", "Caustic" };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderArmorComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<TerrorSpiderArmorComponent> ent, ref DamageModifyEvent args)
    {
        var comp = ent.Comp;

        if (comp.BruteModifier == 1f && comp.BurnModifier == 1f)
            return;

        // Apply Brute modifier to individual types and the group key
        if (comp.BruteModifier != 1f)
        {
            foreach (var type in BruteTypes)
            {
                MultiplyDamageType(args.Damage, type, comp.BruteModifier);
            }

            MultiplyDamageType(args.Damage, "Brute", comp.BruteModifier);
        }

        // Apply Burn modifier to individual types and the group key
        if (comp.BurnModifier != 1f)
        {
            foreach (var type in BurnTypes)
            {
                MultiplyDamageType(args.Damage, type, comp.BurnModifier);
            }

            MultiplyDamageType(args.Damage, "Burn", comp.BurnModifier);
        }
    }

    private static void MultiplyDamageType(DamageSpecifier specifier, string key, float multiplier)
    {
        if (!specifier.DamageDict.TryGetValue(key, out var damage) || damage <= FixedPoint2.Zero)
            return;

        specifier.DamageDict[key] = FixedPoint2.New(damage.Float() * multiplier);
    }
}
