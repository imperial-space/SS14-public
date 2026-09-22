using Content.Server.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticVoidCloakSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticVoidCloakComponent, HereticVoidCloakToggleHoodEvent>(OnToggleHood);
        SubscribeLocalEvent<HereticVoidCloakComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticVoidCloakComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<HereticVoidCloakComponent> ent, ref GotEquippedEvent args)
    {
        ent.Comp.WearerWithImmunity = args.EquipTarget;
        if (!ent.Comp.IsHoodUp)
            EnsureComp<PressureImmunityComponent>(args.EquipTarget);
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnUnequipped(Entity<HereticVoidCloakComponent> ent, ref GotUnequippedEvent args)
    {
        ent.Comp.WearerWithImmunity = null;
        RemComp<PressureImmunityComponent>(args.EquipTarget);
        if (ent.Comp.IsHoodUp)
            ent.Comp.IsHoodUp = false;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnToggleHood(Entity<HereticVoidCloakComponent> ent, ref HereticVoidCloakToggleHoodEvent args)
    {
        if (ent.Comp.WearerWithImmunity is not { } wearer)
            return;

        args.Handled = true;
        ent.Comp.IsHoodUp = !ent.Comp.IsHoodUp;

        if (ent.Comp.IsHoodUp)
        {
            RemComp<PressureImmunityComponent>(wearer);
            if (TryComp<ToggleableClothingComponent>(ent.Owner, out var toggleComp) && toggleComp.ClothingUid != null)
                _inventory.TryEquip(wearer, wearer, toggleComp.ClothingUid.Value, toggleComp.Slot, triggerHandContact: true);
            _popup.PopupEntity(Loc.GetString("heretic-void-cloak-hood-up"), wearer, wearer);
        }
        else
        {
            EnsureComp<PressureImmunityComponent>(wearer);
            if (TryComp<ToggleableClothingComponent>(ent.Owner, out var toggleComp))
                _inventory.TryUnequip(wearer, wearer, toggleComp.Slot, force: true);
            _popup.PopupEntity(Loc.GetString("heretic-void-cloak-hood-down"), wearer, wearer);
        }

        Dirty(ent.Owner, ent.Comp);
    }
}
