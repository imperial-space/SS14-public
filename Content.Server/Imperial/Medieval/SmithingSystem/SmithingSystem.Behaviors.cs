using System.Linq;
using Content.Server.Damage.Components;
using Content.Server.MedievalMeleeResource;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.SmithingSystem;
using Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;
using Content.Shared.Imperial.Medieval.SmithingSystem.Events;
using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.Weapons.Melee;

namespace Content.Server.Imperial.Medieval.SmithingSystem;

public sealed partial class SmithingSystem
{
    private readonly Dictionary<ItemQuality, string> _itemQualityDecorators = new()
    {
        { ItemQuality.Worst, "отвр. " },
        { ItemQuality.ReallyBad, "ужс. " },
        { ItemQuality.Bad, "плох. " },
        { ItemQuality.Default, "хор. " },
        { ItemQuality.Good, "отл. " },
        { ItemQuality.Excellent, "шедевр. " },
    };

    [Dependency] private readonly MedievalMeleeResourceSystem _meleeResource = default!;

    private void InitializeBehaviors()
    {
        SubscribeLocalEvent<UpgradeArmorOnSmithCompleteComponent, SmithingApplyBehaviorsEvent>(UpgradeArmor);
        SubscribeLocalEvent<UpgradeWeaponOnSmithCompleteComponent, SmithingApplyBehaviorsEvent>(UpgradeWeapon);
        SubscribeLocalEvent<DeleteOnLowScoreOnSmithCompleteComponent, SmithingApplyBehaviorsEvent>(DeleteOnLowScore);
    }

    private static DamageModifierSet CopySet(DamageModifierSet src)
    {
        var copy = new DamageModifierSet();

        foreach (var (k, v) in src.Coefficients)
        {
            copy.Coefficients[k] = v;
        }

        foreach (var (k, v) in src.FlatReduction)
        {
            copy.FlatReduction[k] = v;
        }

        return copy;
    }

    private void ApplyQualityToArmor(EntityUid item, float q)
    {
        if (!TryComp<ArmorComponent>(item, out var armor))
            return;

        if (!float.IsFinite(q) || q <= 0f)
            return;

        var baseComp = EnsureComp<SmithArmorBaseComponent>(item);
        if (!baseComp.HasBase)
        {
            baseComp.Base = CopySet(armor.Modifiers);
            baseComp.HasBase = true;
            Dirty(item, baseComp);
        }

        var result = CopySet(baseComp.Base);

        if (!MathHelper.CloseTo(q, 1f))
        {
            foreach (var key in result.FlatReduction.Keys.ToList())
            {
                var flat = result.FlatReduction[key] * q;
                result.FlatReduction[key] = MathF.Round(flat);
            }

            foreach (var key in result.Coefficients.Keys.ToList())
            {
                var coeff = result.Coefficients[key] / q;
                coeff = MathHelper.Clamp(coeff, 0f, 1f);

                var reductionPercent = (1f - coeff) * 100f;
                var roundedPercent = MathF.Round(reductionPercent);

                coeff = 1f - roundedPercent / 100f;
                result.Coefficients[key] = coeff;
            }
        }
        else
        {
            foreach (var key in result.FlatReduction.Keys.ToList())
            {
                result.FlatReduction[key] = MathF.Round(result.FlatReduction[key]);
            }

            foreach (var key in result.Coefficients.Keys.ToList())
            {
                var coeff = MathHelper.Clamp(result.Coefficients[key], 0f, 1f);
                var reductionPercent = (1f - coeff) * 100f;
                var roundedPercent = MathF.Round(reductionPercent);

                coeff = 1f - roundedPercent / 100f;
                result.Coefficients[key] = coeff;
            }
        }

        armor.Modifiers = result;
        Dirty(item, armor);
    }


    private void UpgradeArmor(Entity<UpgradeArmorOnSmithCompleteComponent> ent,
        ref SmithingApplyBehaviorsEvent args)
    {
        if (!TryComp<ArmorComponent>(args.Item, out _))
            return;

        if (TryComp(args.Item, out SmithQualityComponent? existing) && existing.Applied)
            return;

        var modifier = GetBestModifier(args.Score, ent.Comp.ItemQualityTable);

        var qualityComp = EnsureComp<SmithQualityComponent>(args.Item);
        qualityComp.Quality = modifier.Quality;
        qualityComp.Modifier = modifier.Modifier;
        qualityComp.Applied = true;
        Dirty(args.Item, qualityComp);

        ApplyQualityToArmor(args.Item, qualityComp.Modifier);

        SetName(args.Item, modifier.Quality);
    }


    private void UpgradeWeapon(Entity<UpgradeWeaponOnSmithCompleteComponent> ent, ref SmithingApplyBehaviorsEvent args)
    {
        var item = args.Item;


        if (TryComp(item, out SmithQualityComponent? existingQuality) && existingQuality.Applied)
            return;

        var modifier = GetBestModifier(args.Score, ent.Comp.ItemQualityTable);

        if (TryComp(item, out MedievalMeleeResourceComponent? resourceComponent))
        {
            resourceComponent.QualityMultiplier = MathF.Round(modifier.Modifier, 3);

            _meleeResource.RebuildDamageFromBase(item, resourceComponent);
            _meleeResource.RebuildWieldBonusFromBase(item, resourceComponent);
            _meleeResource.CheckResource(item, resourceComponent);

            Dirty(item, resourceComponent);
        }
        else
        {
            if (TryComp(item, out DamageOtherOnHitComponent? damageOtherOnHitComponent))
            {
                damageOtherOnHitComponent.Damage *= modifier.Modifier;
                Dirty(item, damageOtherOnHitComponent);
            }

            if (TryComp(item, out MeleeWeaponComponent? meleeWeaponComponent))
            {
                meleeWeaponComponent.Damage *= modifier.Modifier;
                Dirty(item, meleeWeaponComponent);
            }
        }

        var qualityComp = EnsureComp<SmithQualityComponent>(item);
        qualityComp.Quality = modifier.Quality;
        qualityComp.Modifier = modifier.Modifier;
        qualityComp.Applied = true;
        Dirty(item, qualityComp);

        SetName(item, modifier.Quality);
    }


    private void DeleteOnLowScore(Entity<DeleteOnLowScoreOnSmithCompleteComponent> ent,
        ref SmithingApplyBehaviorsEvent args)
    {
        if (args.Score < ent.Comp.Threshold)
            QueueDel(args.Item);
    }

    private void SetName(EntityUid entityUid, ItemQuality quality)
    {
        var name = Identity.Name(entityUid, EntityManager);

        foreach (var prefix in _itemQualityDecorators.Values)
        {
            if (name.StartsWith(prefix))
            {
                name = name[prefix.Length..];
                break;
            }
        }

        var newName = _itemQualityDecorators[quality] + name;
        _metaDataSystem.SetEntityName(entityUid, newName);
    }

    private SmithQualityModifiers GetBestModifier(int score, Dictionary<int, SmithQualityModifiers> table)
    {
        var bestThreshold = int.MinValue;
        SmithQualityModifiers? best = null;

        foreach (var (threshold, data) in table)
        {
            if (score >= threshold && threshold >= bestThreshold)
            {
                bestThreshold = threshold;
                best = data;
            }
        }

        return best ?? table.MinBy(x => x.Key).Value;
    }
}
