using System.Linq;
using Content.Server.Administration;
using Content.Server.Ghost;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

/// <summary>Ритуалы и испытания книги (spellbook_entries/summons.dm, challenges.dm, code/__HELPERS/wizard_rituals).</summary>
public sealed partial class ImperialSpellbookSpecialsSystem
{
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly QuickDialogSystem _dialog = default!;
    [Dependency] private readonly ImperialListInputSystem _listInput = default!;

    private static readonly SoundSpecifier SummonSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/castsummon.ogg");

    /// <summary>GLOB.summoned_guns.</summary>
    private static readonly EntProtoId[] SummonedGuns =
    {
        "WeaponPistolViper", "WeaponPistolCobra", "WeaponRevolverDeckard", "WeaponRevolverInspector", "WeaponRevolverMateba",
        "WeaponRevolverPython", "WeaponRifleAk", "WeaponRifleLecter", "WeaponShotgunBulldog", "WeaponShotgunDoubleBarreled",
        "WeaponShotgunEnforcer", "WeaponShotgunKammerer", "WeaponShotgunSawn", "WeaponSubMachineGunAtreides", "WeaponSubMachineGunDrozd",
        "WeaponLaserGun", "WeaponLaserCarbine", "WeaponLaserCannon", "WeaponPistolFlintlock",
    };

    /// <summary>GLOB.summoned_magic: посохи, палочки, артефакты.</summary>
    private static readonly EntProtoId[] SummonedMagic =
    {
        "ImperialStaffChange", "ImperialStaffAnimation", "ImperialStaffHealing", "ImperialStaffDoor", "ImperialStaffLocker",
        "ImperialWandDeath", "ImperialWandResurrection", "ImperialWandPolymorph", "ImperialWandTeleport", "ImperialWandDoor",
        "ImperialWandFireball", "ImperialWandShrink", "ImperialScryingOrb", "ImperialNecromanticStone", "ImperialWarpWhistle",
        "ImperialTeleportRod", "ImperialMjolnir", "ImperialSingularityHammer", "ImperialSpellblade", "ImperialStaffChaos",
    };

    /// <summary>curse_of_madness(): «неизлечимые травмы мозга» — берём их аналоги из черт персонажа.</summary>
    private static readonly ProtoId<TraitPrototype>[] MadnessTraits =
    {
        "Narcolepsy", "Paracusia", "PoorVision", "Monochromacy", "SocialAnxiety", "Liar", "FrontalLisp",
        "Snoring", "LightweightDrunk", "Muted", "Pacifist", "ImpairedMobility",
    };

    private bool _summonGuns;
    private bool _summonMagic;
    private bool _massTeachingDone;
    private string? _massTeaching;

    private void InitializeSummons()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnLateJoin);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
    }

    private void OnRestart(RoundRestartCleanupEvent ev)
    {
        _summonGuns = false;
        _summonMagic = false;
        _massTeachingDone = false;
        _massTeaching = null;
    }

    private void HandleSummon(ref ImperialSpellbookSpecialEvent args)
    {
        var user = args.User;
        switch (args.Entry.Special)
        {
            case "Challenge":
                // challenge/can_buy(): испытания Федерация заблокировала.
                args.Handled = true;
                args.Success = false;
                break;
            case "SummonGhosts":
                args.Handled = true;
                args.Success = true;
                _ghost.MakeVisible(true);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/castsummon.ogg"), user);
                break;
            case "SummonGuns":
                args.Handled = true;
                args.Success = true;
                _summonGuns = true;
                foreach (var target in StationHumans(user).ToList())
                    GiveSummoned(target, true);
                _audio.PlayPvs(SummonSound, user);
                break;
            case "SummonMagic":
                args.Handled = true;
                args.Success = true;
                _summonMagic = true;
                foreach (var target in StationHumans(user).ToList())
                    GiveSummoned(target, false);
                _audio.PlayPvs(SummonSound, user);
                break;
            case "SummonEvents":
                args.Handled = true;
                args.Success = true;
                EntityManager.System<ImperialWizardSummonEventsSystem>().Intensify();
                _audio.PlayPvs(SummonSound, user);
                break;
            case "CurseOfMadness":
                args.Handled = true;
                args.Success = StartCurseOfMadness(args.Book, user, args.Entry);
                break;
            case "MassTeaching":
                args.Handled = true;
                args.Success = StartMassTeaching(args.Book, user, args.Entry);
                break;
        }
    }

    /// <summary>Живые люди станции, кроме мага и его прислужников.</summary>
    public IEnumerable<EntityUid> StationHumans(EntityUid? wizard)
    {
        var query = EntityQueryEnumerator<HumanoidProfileComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (uid == wizard || _mobs.IsDead(uid) || _station.GetOwningStation(uid) == null)
                continue;

            if (_mind.TryGetMind(uid, out var mind, out _) && TryComp<ImperialWizardMindComponent>(mind, out var wiz) && !wiz.Survivalist)
                continue;

            yield return uid;
        }
    }

    /// <summary>summon_guns()/summon_magic(): предмет в руки; 10% — стать выживальщиком.</summary>
    public void GiveSummoned(EntityUid target, bool guns, float survivalistChance = 0.1f)
    {
        var item = Spawn(_random.Pick(guns ? SummonedGuns : SummonedMagic), Transform(target).Coordinates);
        _hands.TryPickupAnyHand(target, item);
        _popup.PopupEntity(Loc.GetString("imperial-ritual-summoned-item", ("item", item)), target, target, Content.Shared.Popups.PopupType.Medium);
        if (_random.Prob(survivalistChance))
            _wizard.MakeSurvivalist(target);
    }

    /// <summary>Опоздавшие тоже получают оружие или магию.</summary>
    private void OnLateJoin(PlayerSpawnCompleteEvent ev)
    {
        if (!ev.LateJoin)
            return;

        if (_summonGuns)
            GiveSummoned(ev.Mob, true);
        if (_summonMagic)
            GiveSummoned(ev.Mob, false);
    }

    /// <summary>
    /// curse_of_madness: маг шепчет «тайную истину», все на станции сходят с ума.
    /// Очки списываются только после ввода текста.
    /// </summary>
    private bool StartCurseOfMadness(EntityUid bookUid, EntityUid user, ImperialSpellbookEntryPrototype ritual)
    {
        if (!TryComp<ActorComponent>(user, out var actor) || !TryComp<ImperialSpellbookComponent>(bookUid, out var book))
            return false;

        _dialog.OpenDialog<string>(actor.PlayerSession, Loc.GetString("imperial-ritual-madness-title"), Loc.GetString("imperial-ritual-madness-prompt"),
            message =>
            {
                message = message.Trim();
                if (message.Length == 0 || TerminatingOrDeleted(bookUid) || !_spellbook.CanBuy((bookUid, book), user, ritual))
                    return;

                book.Uses -= ritual.Cost;
                book.Times[ritual.ID] = _spellbook.Times(book, ritual.ID) + 1;
                _wizard.LogPurchase(user, ritual);
                _spellbook.UpdateUi((bookUid, book), user);
                ApplyMadness(user, message);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/mandswap.ogg"), user);
            });
        return false;
    }

    /// <summary>curse_of_madness(): шёпот и неизлечимая странность у каждого на станции.</summary>
    public void ApplyMadness(EntityUid? exclude, string message)
    {
        foreach (var target in StationHumans(exclude).ToList())
        {
            _popup.PopupEntity(Loc.GetString("imperial-ritual-madness-whisper", ("message", message)), target, target, Content.Shared.Popups.PopupType.LargeCaution);
            var trait = _proto.Index(_random.Pick(MadnessTraits));
#pragma warning disable CS0618
            EntityManager.AddComponents(target, trait.Components, false);
#pragma warning restore CS0618
            foreach (var special in trait.Specials)
            {
                special.AfterEquip(target);
            }
        }
    }

    /// <summary>
    /// specific_spell (Mass Wizard Teaching): выбрать запись — её получают все на станции (и маг тоже).
    /// Цена растёт на стоимость выбранной записи.
    /// </summary>
    private bool StartMassTeaching(EntityUid bookUid, EntityUid user, ImperialSpellbookEntryPrototype ritual)
    {
        if (_massTeachingDone || !TryComp<ActorComponent>(user, out var actor) || !TryComp<ImperialSpellbookComponent>(bookUid, out var book))
            return false;

        var options = _spellbook.Entries()
            .Where(e => e.Category != ImperialSpellbookCategory.Rituals && e.Category != ImperialSpellbookCategory.Challenges
                        && e.Category != ImperialSpellbookCategory.Perks && e.Special == null)
            .ToDictionary(e => Loc.GetString(e.Name), e => e);
        _listInput.Open(actor.PlayerSession, Loc.GetString("imperial-ritual-teaching-title"), Loc.GetString("imperial-ritual-teaching-prompt"),
            options.Keys.OrderBy(k => k).ToList(), choice =>
            {
                if (!options.TryGetValue(choice, out var chosen) || _massTeachingDone || TerminatingOrDeleted(bookUid))
                    return;

                var total = ritual.Cost + chosen.Cost;
                if (book.Uses < total)
                {
                    _popup.PopupEntity(Loc.GetString("imperial-ritual-teaching-poor", ("cost", total)), user, user);
                    return;
                }

                book.Uses -= total;
                book.Times[ritual.ID] = 1;
                _massTeachingDone = true;
                _massTeaching = chosen.ID;
                foreach (var target in StationHumans(user).Append(user).ToList())
                {
                    if (chosen.Spell is { } spell)
                        _spellbook.GrantSpell(target, spell);
                    foreach (var item in chosen.Items)
                    {
                        var spawned = Spawn(item, Transform(target).Coordinates);
                        _hands.TryPickupAnyHand(target, spawned);
                    }
                }

                _popup.PopupEntity(Loc.GetString(chosen.Spell != null ? "imperial-ritual-teaching-spell" : "imperial-ritual-teaching-item", ("name", choice)),
                    user, user, Content.Shared.Popups.PopupType.Large);
                _wizard.LogPurchase(user, ritual);
                _spellbook.UpdateUi((bookUid, book), user);
                Log.Info($"{ToPrettyString(user)} научил всех «{choice}» (массовое обучение)");
            });

        // Очки списываются после выбора, сама запись бесплатна.
        return false;
    }
}
