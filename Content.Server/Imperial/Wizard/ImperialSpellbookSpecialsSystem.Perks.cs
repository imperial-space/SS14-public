using System.Linq;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Wizard;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

/// <summary>Перки книги (spellbook_entries/perks.dm): выдаются навсегда, возврата нет.</summary>
public sealed partial class ImperialSpellbookSpecialsSystem
{
    [Dependency] private readonly ImperialWizardRuleSystem _wizard = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly string[] Perks =
    {
        "ImperialSpellbookPerkFourHands", "ImperialSpellbookPerkWormborn", "ImperialSpellbookPerkDejavu",
        "ImperialSpellbookPerkSpellLottery", "ImperialSpellbookPerkHeartEater", "ImperialSpellbookPerkSlimeFriends",
        "ImperialSpellbookPerkTransparence", "ImperialSpellbookPerkMagnetism",
    };

    private void InitializePerks()
    {
    }

    private void HandlePerk(ref ImperialSpellbookSpecialEvent args)
    {
        if (args.Entry.Category != ImperialSpellbookCategory.Perks)
            return;

        args.Handled = true;
        if (args.Entry.Special == "Gamble")
        {
            args.Success = Gamble(args.User);
            return;
        }

        args.Success = GivePerk(args.User, args.Entry.ID);
    }

    /// <summary>perks/buy_spell(): перк запоминается на разуме и сразу действует.</summary>
    public bool GivePerk(EntityUid user, string perk)
    {
        _wizard.AddPerk(user, perk);
        var name = _proto.TryIndex<ImperialSpellbookEntryPrototype>(perk, out var entry) ? Loc.GetString(entry.Name) : perk;
        _popup.PopupEntity(Loc.GetString("imperial-perk-gained", ("perk", name)), user, user);

        switch (perk)
        {
            case "ImperialSpellbookPerkFourHands":
                // change_number_of_hands(4)
                _hands.AddHand(user, "imperial-wizard-hand-3", HandLocation.Left);
                _hands.AddHand(user, "imperial-wizard-hand-4", HandLocation.Right);
                break;
            case "ImperialSpellbookPerkWormborn":
                EnsureComp<ImperialWormbornComponent>(user);
                break;
            case "ImperialSpellbookPerkDejavu":
                EnsureComp<ImperialDejavuComponent>(user);
                break;
            case "ImperialSpellbookPerkHeartEater":
                EnsureComp<ImperialHeartEaterComponent>(user);
                break;
            case "ImperialSpellbookPerkSlimeFriends":
                EnsureComp<ImperialSlimeFriendsComponent>(user);
                break;
            case "ImperialSpellbookPerkTransparence":
                EntityManager.System<ImperialWizardPerksSystem>().MakeTransparent(user);
                break;
            case "ImperialSpellbookPerkMagnetism":
                EnsureComp<ImperialMagnetismComponent>(user);
                break;
        }

        return true;
    }

    /// <summary>gamble: два случайных ещё не изученных перка; если их нет — очки возвращаются.</summary>
    private bool Gamble(EntityUid user)
    {
        var available = Perks.Where(p => !_wizard.HasPerk(user, p)).ToList();
        if (available.Count < 1)
        {
            _popup.PopupEntity(Loc.GetString("imperial-perk-gamble-failed"), user, user);
            return false;
        }

        _random.Shuffle(available);
        foreach (var perk in available.Take(2))
        {
            GivePerk(user, perk);
        }

        return true;
    }
}
