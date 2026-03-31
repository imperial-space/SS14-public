using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Cult.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Система мантии флагелланта.
/// При надевании добавляет носителю <see cref="FlagellantRobesEffectComponent"/>,
/// который удваивает весь входящий урон через <see cref="DamageModifyEvent"/>.
/// </summary>
public sealed class FlagellantRobesSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlagellantRobesComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<FlagellantRobesComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<FlagellantRobesEffectComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnEquipped(Entity<FlagellantRobesComponent> ent, ref ClothingGotEquippedEvent args)
    {
        var effect = EnsureComp<FlagellantRobesEffectComponent>(args.Wearer);
        effect.DamageMultiplier = ent.Comp.DamageMultiplier;
    }

    private void OnUnequipped(Entity<FlagellantRobesComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        RemComp<FlagellantRobesEffectComponent>(args.Wearer);
    }

    private void OnDamageModify(EntityUid uid, FlagellantRobesEffectComponent comp, DamageModifyEvent args)
    {
        if (args.Damage.Empty)
            return;

        var doubled = new DamageSpecifier();
        foreach (var (key, amount) in args.Damage.DamageDict)
            doubled.DamageDict[key] = amount * (FixedPoint2)comp.DamageMultiplier;
        args.Damage = doubled;
    }
}
