using Content.Server.Imperial.Wizard.Spells;
using Content.Shared.Eye;
using Content.Shared.Inventory;
using Content.Shared.Popups;

namespace Content.Server.Imperial.Wizard;

/// <summary>
/// Особые записи книги (buy_spell() с переопределением): камни душ с заклинанием оболочки,
/// шар предсказаний с вечным «рентгеном», доспехи боевого мага.
/// </summary>
public sealed partial class ImperialSpellbookSpecialsSystem : EntitySystem
{
    [Dependency] private readonly ImperialSpellbookSystem _spellbook = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialSpellbookSpecialEvent>(OnSpecial);
        InitializePerks();
        InitializeSummons();
    }

    private void OnSpecial(ref ImperialSpellbookSpecialEvent args)
    {
        if (args.Handled)
            return;

        switch (args.Entry.Special)
        {
            case "Soulstones":
                // soulstones/buy_spell(): в придачу — заклинание оболочки конструкта.
                args.Handled = true;
                args.Success = true;
                _spellbook.GrantSpell(args.User, "ImperialSpellConstructShell");
                break;
            case "ScryingXray":
                // Покупка шара навсегда даёт рентгеновское зрение.
                args.Handled = true;
                args.Success = true;
                EnsureComp<ImperialPermanentXrayComponent>(args.User);
                _eye.SetDrawFov(args.User, false);
                break;
            case "Armor":
                args.Handled = true;
                args.Success = EquipArmor(args.User);
                break;
            default:
                HandlePerk(ref args);
                if (!args.Handled)
                    HandleSummon(ref args);
                break;
        }
    }

    /// <summary>armor/try_equip_item(): старая мантия и шляпа падают, доспехи надеваются.</summary>
    private bool EquipArmor(EntityUid user)
    {
        var armor = Spawn("ImperialBattlemageArmor", Transform(user).Coordinates);
        _inventory.TryUnequip(user, "outerClothing", true, true);
        _inventory.TryUnequip(user, "head", true, true);
        if (!_inventory.TryEquip(user, armor, "outerClothing", true, true))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spellbook-armor-failed"), user, user);
            return true;
        }

        return true;
    }
}
