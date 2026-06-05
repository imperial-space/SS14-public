using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Weapons.Melee;

namespace Content.Shared.Imperial.Lavaland.Weapons;

/// <summary>
/// Меняет AttackRate у MeleeWeapon при переключении режимов боевого секача.
/// </summary>
public sealed class CleavingSawSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CleavingSawComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnToggled(Entity<CleavingSawComponent> ent, ref ItemToggledEvent args)
    {
        if (!TryComp<MeleeWeaponComponent>(ent, out var melee))
            return;

        melee.AttackRate = args.Activated
            ? ent.Comp.ActivatedAttackRate
            : ent.Comp.DeactivatedAttackRate;

        Dirty(ent, melee);
    }
}
