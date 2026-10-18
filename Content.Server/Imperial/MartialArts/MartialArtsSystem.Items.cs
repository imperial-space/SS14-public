using Robust.Shared.Random;
using Content.Server.GameTicking;
using Content.Shared.DoAfter;
using Content.Shared.Explosion.Components;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.MartialArts;
using Content.Shared.Implants;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>
/// Предметы боевых искусств: одежда-учителя, импланты, учебники и свитки, странный гриб, посох бо,
/// CQC повара и перчатки Каза Рук в шкафу смотрителя.
/// </summary>
public sealed partial class MartialArtsSystem
{
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;

    /// <summary>Ванильные боксёрские перчатки SS14 учат боксу, как /obj/item/clothing/gloves/boxing.</summary>
    private static readonly Dictionary<string, string> VanillaGivers = new()
    {
        { "ClothingHandsGlovesBoxingRed", "MartialArtBoxing" },
        { "ClothingHandsGlovesBoxingBlue", "MartialArtBoxing" },
        { "ClothingHandsGlovesBoxingYellow", "MartialArtBoxing" },
        { "ClothingHandsGlovesBoxingGreen", "MartialArtBoxing" },
        { "ClothingBeltChampion", "MartialArtWrestling" },
    };

    /// <summary>secure_closet/warden → garment bag: перчатки Каза Рук.</summary>
    private static readonly string[] WardenLockers = ["LockerWardenFilled", "LockerWardenFilledHardsuit"];

    private static readonly EntProtoId WardenGloves = "ClothingHandsGlovesKazaRukSec";
    private static readonly ProtoId<MartialArtPrototype> ChefCqc = "MartialArtCloseQuartersCooking";

    private static readonly SoundPathSpecifier PageSound = new("/Audio/Effects/rustle1.ogg");

    private void InitializeItems()
    {
        InitializeKazaJudo();

        SubscribeLocalEvent<InventoryComponent, DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<InventoryComponent, DidUnequipEvent>(OnDidUnequip);

        SubscribeLocalEvent<MartialArtImplantComponent, ImplantImplantedEvent>(OnImplanted);
        SubscribeLocalEvent<MartialArtImplantComponent, ImplantRemovedEvent>(OnImplantRemoved);
        SubscribeLocalEvent<MartialArtImplantComponent, MartialArtsImplantToggleEvent>(OnImplantToggle);

        SubscribeLocalEvent<MartialArtManualComponent, UseInHandEvent>(OnManualUse);
        SubscribeLocalEvent<MartialArtManualComponent, MartialManualReadDoAfterEvent>(OnManualPage);

        SubscribeLocalEvent<MartialArtFoodComponent, UseInHandEvent>(OnMushroomUse);
        SubscribeLocalEvent<BoStaffComponent, MeleeHitEvent>(OnBoStaffHit);
        SubscribeLocalEvent<BoStaffComponent, AttemptMeleeEvent>(OnBoStaffAttempt);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<PostGameMapLoad>(OnMapLoaded);
    }

    #region Одежда

    /// <summary>martial_art_giver: надел — учишь, снял — забываешь.</summary>
    private void OnDidEquip(Entity<InventoryComponent> ent, ref DidEquipEvent args)
    {
        if ((args.SlotFlags & SlotFlags.POCKET) != 0)
            return;

        ProtoId<MartialArtPrototype> art;
        if (TryComp<MartialArtGiverComponent>(args.Equipment, out var giver))
        {
            art = giver.Art;
        }
        else if (MetaData(args.Equipment).EntityPrototype?.ID is { } id && VanillaGivers.TryGetValue(id, out var vanillaArt))
        {
            giver = EnsureComp<MartialArtGiverComponent>(args.Equipment);
            giver.Art = vanillaArt;
            art = vanillaArt;
        }
        else
        {
            return;
        }

        giver.Wearer = ent.Owner;
        Teach(ent, art, args.Equipment);
    }

    private void OnDidUnequip(Entity<InventoryComponent> ent, ref DidUnequipEvent args)
    {
        if (!TryComp<MartialArtGiverComponent>(args.Equipment, out var giver) || giver.Wearer != ent.Owner)
            return;

        giver.Wearer = null;
        Unlearn(ent, giver.Art, args.Equipment);
    }

    #endregion

    #region Импланты

    /// <summary>implant/activate(): автоматически срабатывает при вживлении.</summary>
    private void OnImplanted(Entity<MartialArtImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        Teach(args.Implanted, ent.Comp.Art, ent);
    }

    private void OnImplantRemoved(Entity<MartialArtImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        Unlearn(args.Implanted, ent.Comp.Art, ent);
    }

    /// <summary>activate(): если уже умеешь — забыть, иначе выучить.</summary>
    private void OnImplantToggle(Entity<MartialArtImplantComponent> ent, ref MartialArtsImplantToggleEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.Performer;
        if (TryComp<MartialArtsComponent>(user, out var comp) && comp.Arts.Exists(a => a.Art == ent.Comp.Art && a.Source == ent.Owner))
            Unlearn(user, ent.Comp.Art, ent);
        else
            Teach(user, ent.Comp.Art, ent);
    }

    #endregion

    #region Учебники

    /// <summary>granter/attack_self(): три страницы по 5 секунд и ещё 5 секунд на усвоение.</summary>
    private void OnManualUse(Entity<MartialArtManualComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;

        if (IsBlind(user))
        {
            _popup.PopupEntity(Loc.GetString("martial-manual-blind"), user, user, PopupType.SmallCaution);
            return;
        }

        if (Knows(user, ent.Comp.Art))
        {
            _popup.PopupEntity(Loc.GetString("martial-manual-known", ("art", Loc.GetString(ent.Comp.MartialName))), user, user, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.Uses <= 0)
        {
            ManualRecoil(ent, user);
            return;
        }

        ent.Comp.PagesRead = 0;
        _popup.PopupEntity(Loc.GetString("martial-manual-start", ("art", Loc.GetString(ent.Comp.MartialName))), user, user);
        StartPage(ent, user);
    }

    private void StartPage(Entity<MartialArtManualComponent> ent, EntityUid user)
    {
        _audio.PlayPvs(PageSound, user);
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.PageTime, new MartialManualReadDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnManualPage(Entity<MartialArtManualComponent> ent, ref MartialManualReadDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("martial-manual-stop"), user, user);
            return;
        }

        ent.Comp.PagesRead++;

        // turn_page(): pages_to_mastery страниц, затем последнее do_after.
        if (ent.Comp.PagesRead <= ent.Comp.Pages)
        {
            var remark = ent.Comp.Remarks.Count > 0 ? Loc.GetString(_random.Pick(ent.Comp.Remarks)) : Loc.GetString("martial-manual-continue");
            _popup.PopupEntity(remark, user, user);
            StartPage(ent, user);
            return;
        }

        ent.Comp.Uses--;
        Teach(user, ent.Comp.Art);
        Notify(user, Loc.GetString(ent.Comp.Greet));

        if (ent.Comp.Uses > 0)
            return;

        // Свитки после прочтения пустеют, учебник CQC зловеще пищит.
        if (ent.Comp.SpentProto is { } spent)
        {
            var coords = Transform(ent).Coordinates;
            var blank = Spawn(spent, coords);
            QueueDel(ent);
            _hands.TryPickupAnyHand(user, blank);
        }
        else if (ent.Comp.Recoil == MartialManualRecoil.Explode)
        {
            _popup.PopupEntity(Loc.GetString("martial-manual-beeps", ("manual", ent.Owner)), user, user, PopupType.MediumCaution);
        }
    }

    /// <summary>recoil(): учебник CQC взрывается, если читать его второй раз.</summary>
    private void ManualRecoil(Entity<MartialArtManualComponent> ent, EntityUid user)
    {
        if (ent.Comp.Recoil != MartialManualRecoil.Explode)
        {
            _popup.PopupEntity(Loc.GetString("martial-manual-empty"), user, user, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("martial-manual-explodes", ("manual", ent.Owner)), user, user, PopupType.LargeCaution);
        Sound(user, "/Audio/Effects/explosion1.ogg", -6f);
        Blind(user, TimeSpan.FromSeconds(1));
        Brute(user, 6, null);
        Brute(user, 6, null, "Heat");
        QueueDel(ent);
    }

    #endregion

    #region Гриб

    /// <summary>mushpunch/attack_self(): проглотить гриб и навсегда получить грибной удар.</summary>
    private void OnMushroomUse(Entity<MartialArtFoodComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString(ent.Comp.Message, ("item", ent.Owner)), args.User, args.User, PopupType.Medium);
        PopupPair(args.User, args.User, "martial-mushroom-devour-others", "martial-mushroom-devour-self");
        Teach(args.User, ent.Comp.Art);
        QueueDel(ent);
    }

    #endregion

    #region Посох бо

    /// <summary>
    /// bostaff/attack(): неуклюжий бьёт себя, лежачих бить бесчестно,
    /// толчок двумя руками бьёт по выносливости и может вырубить.
    /// </summary>
    private void OnBoStaffHit(Entity<BoStaffComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var user = args.User;
        if (HasComp<Content.Shared.Clumsy.ClumsyComponent>(user) && _random.Prob(0.5f))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("martial-bostaff-clumsy"), user, user, PopupType.MediumCaution);
            Paralyze(user, TimeSpan.FromSeconds(6));
            Brute(user, args.BaseDamage.GetTotal().Float() * 2, null);
            return;
        }

        var target = args.HitEntities[0];
        if (!HasComp<Content.Shared.Mobs.Components.MobStateComponent>(target))
            return;

        if (!IsConscious(target))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("martial-bostaff-dishonor"), user, user, PopupType.SmallCaution);
            return;
        }

    }

    /// <summary>Правый клик двумя руками — тычок посохом вместо толчка.</summary>
    private void OnBoStaffAttempt(Entity<BoStaffComponent> ent, ref AttemptMeleeEvent args)
    {
        if (Transform(ent).ParentUid is not { Valid: true } user || !TryTakePendingDisarm(user, out var target))
            return;

        if (!TryComp<WieldableComponent>(ent, out var wield) || !wield.Wielded || !IsHuman(target)
            || !IsConscious(target) || !_xform.InRange(user, target, MeleeRange))
            return;

        args.Cancelled = true;
        BoStaffPoke(user, target);
    }

    private void BoStaffPoke(EntityUid user, EntityUid target)
    {
        var verb = _random.Pick(new[] { "1", "2", "3", "4", "5" });
        PopupPair(user, target, $"martial-bostaff-poke-{verb}-others", $"martial-bostaff-poke-{verb}-self", $"martial-bostaff-poke-{verb}-target");
        Sound(user, "/Audio/Imperial/MartialArts/woodhit.ogg");
        Stamina(target, _random.Next(13, 21), user);

        if (_random.Prob(0.1f))
        {
            PopupPair(target, target, "martial-bostaff-fall-others", "martial-bostaff-fall-self");
            Paralyze(target, TimeSpan.FromSeconds(8));
        }

        if (StaminaLoss(target) > 0 && !HasComp<Content.Shared.Bed.Sleep.SleepingComponent>(target) && IsStaminaCrit(target))
        {
            PopupPair(user, target, "martial-bostaff-knockout-others", "martial-bostaff-knockout-self", "martial-bostaff-knockout-target", PopupType.LargeCaution);
            Sleep(target, TimeSpan.FromSeconds(60));
        }
    }

    #endregion

    #region Роли

    /// <summary>skillchip/job/chef: повар умеет защищать свою кухню.</summary>
    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == "Chef")
            Teach(args.Mob, ChefCqc);
    }

    /// <summary>garment bag смотрителя: перчатки Каза Рук.</summary>
    private void OnMapLoaded(PostGameMapLoad args)
    {
        var query = EntityQueryEnumerator<EntityStorageComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var storage, out var meta, out var xform))
        {
            if (meta.EntityPrototype?.ID is not { } id || Array.IndexOf(WardenLockers, id) < 0)
                continue;

            if (xform.MapID != args.Map)
                continue;

            var gloves = Spawn(WardenGloves, xform.Coordinates);
            if (!_entityStorage.Insert(gloves, uid, storage))
                QueueDel(gloves);
        }
    }

    #endregion
}
