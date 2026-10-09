using Content.Server.Actions;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Bible.Components;
using Content.Server.Chat.Managers;
using Content.Server.Imperial.NullRod;
using Content.Shared.Clumsy;
using Content.Shared.Database;
using Content.Shared.Dataset;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Chaplain;
using Content.Shared.Imperial.Chaplain.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Chaplain;

/// <summary>
/// Религия станции и святые роли (holy_role из SS13): верховный жрец выбирает религию, бога, название
/// и обложку библии, жрецы и дьяконы присоединяются к ней. Роль и снаряжение выдаются командой <c>makechaplain</c>.
/// </summary>
public sealed class ImperialReligionSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly NullRodSystem _nullRod = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const string HolyFaction = "ImperialHoly";
    private static readonly EntProtoId PrayAction = "ActionImperialPray";
    private static readonly ProtoId<DatasetPrototype> ReligionNames = "ImperialReligionNames";

    // Снаряжение капеллана из SS13 (datum/outfit/job/chaplain + after_spawn).
    private static readonly EntProtoId Uniform = "ClothingUniformJumpsuitChaplain";
    private static readonly EntProtoId Backpack = "ClothingBackpackImperialCultpack";
    private static readonly EntProtoId Bible = "BibleImperialChaplain";
    private static readonly EntProtoId Whiskey = "DrinkWhiskeyBottleFull";
    private static readonly EntProtoId NullRodProto = "NullRod";
    private static readonly EntProtoId Stamp = "RubberStampChaplain";
    private static readonly EntProtoId ArmamentsBeacon = "HolyArmamentsBeacon";
    private static readonly EntProtoId Vestments = "BoxImperialChaplainVestments";

    // Последствия обложек «Clown Bible», «Banana Bible» и «Insulationism».
    private static readonly EntProtoId ClownMask = "ClothingMaskClown";
    private static readonly EntProtoId Insuls = "ClothingHandsGlovesColorYellowBudget";

    /// <summary>Установлена ли уже религия станции (есть ли верховный жрец).</summary>
    public bool ReligionEstablished { get; private set; }

    public string Religion { get; private set; } = string.Empty;
    public string Deity { get; private set; } = string.Empty;
    public string BibleName { get; private set; } = string.Empty;

    /// <summary>Описание библий, если религия его меняет (servicianism в SS13).</summary>
    public string? BibleDescription { get; private set; }

    /// <summary>Обложка библий станции (GLOB.bible_icon_state в SS13).</summary>
    public string? BibleSkin { get; private set; }

    /// <summary>Набор святой брони станции (GLOB.holy_armor_type в SS13).</summary>
    public EntProtoId? StationArmamentsKit { get; set; }

    /// <summary>Верховный жрец, который ещё не выбрал названия религии.</summary>
    private EntityUid? _pendingHighPriest;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<ImperialHolyComponent, ImperialPrayActionEvent>(OnPrayAction);
        SubscribeLocalEvent<ImperialHolyBibleComponent, ExaminedEvent>(OnBibleExamined);
        SubscribeLocalEvent<ImperialHolyBibleComponent, UseInHandEvent>(OnBibleUseInHand);
        SubscribeLocalEvent<ImperialHolyBibleComponent, ImperialBibleSkinPickMessage>(OnBibleSkinPicked);
        SubscribeNetworkEvent<ImperialReligionSetupSubmitEvent>(OnSetupSubmit);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        ReligionEstablished = false;
        Religion = string.Empty;
        Deity = string.Empty;
        BibleName = string.Empty;
        BibleDescription = null;
        BibleSkin = null;
        StationArmamentsKit = null;
        _pendingHighPriest = null;
    }

    /// <summary>
    /// Делает существо святым и выдаёт снаряжение. Верховному жрецу открывается окно выбора религии.
    /// </summary>
    /// <param name="role">Роль. Если null — верховный жрец, пока религия не установлена, иначе жрец.</param>
    public HolyRole MakeHoly(EntityUid uid, HolyRole? role = null)
    {
        var finalRole = role ?? (ReligionEstablished ? HolyRole.Priest : HolyRole.HighPriest);

        // Пока верховный жрец не выбрал названия, у религии станции значения по умолчанию.
        var establishing = finalRole == HolyRole.HighPriest || !ReligionEstablished;
        if (establishing)
        {
            ApplyReligion(Loc.GetString("imperial-religion-default-religion"),
                Loc.GetString("imperial-religion-default-deity"),
                Loc.GetString("imperial-religion-default-bible"));
            ReligionEstablished = true;
        }

        var holy = EnsureComp<ImperialHolyComponent>(uid);
        holy.Role = finalRole;
        Dirty(uid, holy);

        EnsureComp<BibleUserComponent>(uid);
        _faction.AddFaction(uid, HolyFaction);
        if (holy.PrayAction == null)
            _actions.AddAction(uid, ref holy.PrayAction, PrayAction);

        if (finalRole != HolyRole.Deacon)
            GiveEquipment(uid);

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(uid):player} became holy ({finalRole})");

        if (establishing && TryComp<ActorComponent>(uid, out var actor))
        {
            OpenReligionSetup(uid, actor.PlayerSession);
            return finalRole;
        }

        Greet(uid, finalRole);
        return finalRole;
    }

    private void OpenReligionSetup(EntityUid uid, ICommonSession session)
    {
        _pendingHighPriest = uid;

        // Как в настройках SS13: название религии по умолчанию случайное из списка.
        var religion = _proto.TryIndex(ReligionNames, out var names) && names.Values.Count > 0
            ? _random.Pick(names.Values)
            : Loc.GetString("imperial-religion-default-religion");

        RaiseNetworkEvent(new ImperialReligionSetupOpenEvent(religion,
                Loc.GetString("imperial-religion-default-deity"),
                Loc.GetString("imperial-religion-default-bible")),
            session);
    }

    private void OnSetupSubmit(ImperialReligionSetupSubmitEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid
            || uid != _pendingHighPriest
            || !TryComp<ImperialHolyComponent>(uid, out var holy))
        {
            return;
        }

        _pendingHighPriest = null;
        ApplyReligion(Clean(ev.Religion), Clean(ev.Deity), Clean(ev.Bible));

        // Библии, выданные до выбора, получают новые названия.
        var query = EntityQueryEnumerator<ImperialHolyBibleComponent>();
        while (query.MoveNext(out var bible, out var comp))
        {
            SetupBible((bible, comp));
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(uid):player} established religion {Religion}, deity {Deity}, bible {BibleName}");
        Greet(uid, holy.Role);
    }

    private static string Clean(string text)
    {
        text = text.Trim();
        return text.Length > ImperialBibleSkins.MaxNameLength ? text[..ImperialBibleSkins.MaxNameLength] : text;
    }

    /// <summary>
    /// Устанавливает религию станции вместе с шуточными религиями из SS13 (chaplain/after_spawn).
    /// </summary>
    private void ApplyReligion(string religion, string deity, string bible)
    {
        if (string.IsNullOrEmpty(religion))
            religion = Loc.GetString("imperial-religion-default-religion");
        if (string.IsNullOrEmpty(deity))
            deity = Loc.GetString("imperial-religion-default-deity");

        BibleDescription = null;
        switch (religion.ToLowerInvariant())
        {
            case "lol" or "wtf" or "poo" or "badmin" or "shitmin" or "deadmin" or "meme" or "memes" or "skibidi"
                or "лол" or "втф" or "мем" or "мемы" or "скибиди":
                bible = _random.Pick(new[]
                {
                    "Woody's Got Wood: The Aftermath",
                    "Sweet Bro and Hella Jeff: Expanded Edition",
                    "F.A.T.A.L. Rulebook",
                    "Toilet Humor",
                });
                deity = bible switch
                {
                    "Woody's Got Wood: The Aftermath" => _random.Pick(new[] { "Woody", "Andy", "Cherry Flavored Lube" }),
                    "Sweet Bro and Hella Jeff: Expanded Edition" => _random.Pick(new[] { "Sweet Bro", "Hella Jeff", "Stairs", "AH" }),
                    "F.A.T.A.L. Rulebook" => "Twenty Ten-Sided Dice",
                    _ => _random.Pick(new[] { "Skibidi Toilet", "Skibidi Wizard", "Skibidi Bathtub", "John Skibidi", "Skibidi Skibidi", "G-Toilet 1.0", "John Freeman" }),
                };
                break;
            case "servicianism" or "partying" or "сервицианизм" or "вечеринки":
                BibleDescription = Loc.GetString("imperial-religion-servicianism-bible-desc");
                break;
            case "weeaboo" or "kawaii" or "виабу" or "каваи":
                bible = _random.Pick(new[]
                {
                    "Fanfiction Compendium",
                    "Japanese for Dummies",
                    "The Manganomicon",
                    "Establishing Your O.T.P",
                });
                deity = "Anime";
                break;
        }

        if (string.IsNullOrEmpty(bible) || bible == Loc.GetString("imperial-religion-default-bible"))
            bible = Loc.GetString("imperial-religion-bible-replace", ("religion", religion));

        Religion = religion;
        Deity = deity;
        BibleName = bible;
    }

    private void Greet(EntityUid uid, HolyRole role)
    {
        var messageKey = role switch
        {
            HolyRole.HighPriest => "imperial-religion-greeting-high-priest",
            HolyRole.Priest => "imperial-religion-greeting-priest",
            _ => "imperial-religion-greeting-deacon",
        };
        var message = Loc.GetString(messageKey, ("religion", Religion), ("deity", Deity));
        _popup.PopupEntity(message, uid, uid, PopupType.Large);
        if (TryComp<ActorComponent>(uid, out var actor))
            _chatManager.DispatchServerMessage(actor.PlayerSession, message);
    }

    /// <summary>
    /// Снаряжение капеллана: форма и рюкзак на свободные слоты, библия с виски, святое оружие станции,
    /// печать, маяк вооружения и коробка облачений.
    /// </summary>
    private void GiveEquipment(EntityUid uid)
    {
        var coords = Transform(uid).Coordinates;

        if (!_inventory.TryGetSlotEntity(uid, "jumpsuit", out _))
            _inventory.TryEquip(uid, Spawn(Uniform, coords), "jumpsuit", silent: true, force: true);

        if (!_inventory.TryGetSlotEntity(uid, "back", out _))
            _inventory.TryEquip(uid, Spawn(Backpack, coords), "back", silent: true, force: true);
        else
            GiveItem(uid, Spawn(Backpack, coords));

        // Библия религии станции с бутылкой виски внутри (bible/booze).
        var bible = Spawn(Bible, coords);
        SetupBible((bible, EnsureComp<ImperialHolyBibleComponent>(bible)));
        if (BibleSkin != null)
            SetBibleSkin(bible, BibleSkin);
        if (TryComp<StorageComponent>(bible, out var bibleStorage))
            _storage.Insert(bible, Spawn(Whiskey, coords), out _, storageComp: bibleStorage, playSound: false);
        GiveItem(uid, bible);

        // Святое оружие станции, если его уже выбрали, иначе нулевой стержень.
        var weapon = Spawn(_nullRod.StationHolyWeapon ?? NullRodProto, coords);
        if (!_hands.TryPickupAnyHand(uid, weapon))
            GiveItem(uid, weapon);

        GiveItem(uid, Spawn(Stamp, coords));
        GiveItem(uid, Spawn(ArmamentsBeacon, coords));
        GiveItem(uid, Spawn(Vestments, coords));
    }

    private void SetupBible(Entity<ImperialHolyBibleComponent> bible)
    {
        _metaData.SetEntityName(bible, BibleName);
        if (BibleDescription != null)
            _metaData.SetEntityDescription(bible, BibleDescription);
        bible.Comp.Deity = Deity;
    }

    /// <summary>Кладёт предмет в рюкзак, иначе в руки, иначе оставляет под ногами.</summary>
    private void GiveItem(EntityUid uid, EntityUid item)
    {
        if (_inventory.TryGetSlotEntity(uid, "back", out var back)
            && TryComp<StorageComponent>(back, out var storage)
            && _storage.Insert(back.Value, item, out _, user: uid, storageComp: storage, playSound: false))
        {
            return;
        }

        _hands.PickupOrDrop(uid, item);
    }

    /// <summary>Обложку выбирает только верховный жрец и только один раз за раунд (can_set_bible_skin).</summary>
    private bool CanSetBibleSkin(EntityUid user)
    {
        return BibleSkin == null
            && TryComp<ImperialHolyComponent>(user, out var holy)
            && holy.Role == HolyRole.HighPriest;
    }

    private void OnBibleUseInHand(Entity<ImperialHolyBibleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !CanSetBibleSkin(args.User))
            return;

        args.Handled = _ui.TryOpenUi(ent.Owner, ImperialBibleSkinUiKey.Key, args.User);
    }

    private void OnBibleSkinPicked(Entity<ImperialHolyBibleComponent> ent, ref ImperialBibleSkinPickMessage args)
    {
        var user = args.Actor;
        if (!CanSetBibleSkin(user)
            || !_hands.IsHolding(user, ent)
            || !ImperialBibleSkins.TryGetHeldPrefix(args.Skin, out _))
        {
            return;
        }

        _ui.CloseUi(ent.Owner, ImperialBibleSkinUiKey.Key);
        BibleSkin = args.Skin;
        SetBibleSkin(ent, args.Skin);

        switch (args.Skin)
        {
            case "honk1" or "honk2":
                EnsureComp<ClumsyComponent>(user);
                EquipOrDelete(user, ClownMask, "mask");
                break;
            case "insuls":
                var gloves = Spawn(Insuls, Transform(user).Coordinates);
                _metaData.SetEntityName(gloves, Loc.GetString("imperial-religion-insuls-name"));
                _metaData.SetEntityDescription(gloves, Loc.GetString("imperial-religion-insuls-desc"));
                if (!_inventory.TryEquip(user, gloves, "gloves", silent: true))
                    GiveItem(user, gloves);
                break;
        }

        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} chose bible skin {args.Skin}");
    }

    private void EquipOrDelete(EntityUid user, EntProtoId proto, string slot)
    {
        var item = Spawn(proto, Transform(user).Coordinates);
        if (!_inventory.TryEquip(user, item, slot, silent: true))
            Del(item);
    }

    private void SetBibleSkin(EntityUid bible, string skin)
    {
        if (!ImperialBibleSkins.TryGetHeldPrefix(skin, out var prefix))
            return;

        _appearance.SetData(bible, ImperialBibleSkinVisuals.Skin, skin);
        _item.SetHeldPrefix(bible, prefix);
    }

    private void OnPrayAction(Entity<ImperialHolyComponent> ent, ref ImperialPrayActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        var session = actor.PlayerSession;
        _quickDialog.OpenDialog(session,
            Loc.GetString("imperial-religion-pray-title"),
            Loc.GetString("prayer-popup-notify-pray-ui-message"),
            (string message) => SendPrayer(session, message));
    }

    /// <summary>Молитва святого: администрация видит её с пометкой капеллана (CHAPLAIN PRAYER в SS13).</summary>
    private void SendPrayer(ICommonSession session, string message)
    {
        if (session.AttachedEntity is not { } uid || !HasComp<ImperialHolyComponent>(uid))
            return;

        var prefix = Loc.GetString("imperial-religion-pray-prefix");
        _popup.PopupEntity(Loc.GetString("imperial-religion-pray-sent", ("deity", Deity)), uid, session, PopupType.Medium);
        _chatManager.SendAdminAnnouncement($"{prefix} <{session.Name}>: {message}");
        _adminLog.Add(LogType.AdminMessage, LogImpact.Low, $"{ToPrettyString(uid):player} sent prayer ({prefix}): {message}");
    }

    private void OnBibleExamined(Entity<ImperialHolyBibleComponent> ent, ref ExaminedEvent args)
    {
        if (!string.IsNullOrEmpty(ent.Comp.Deity))
            args.PushMarkup(Loc.GetString("imperial-religion-bible-examine", ("deity", ent.Comp.Deity)));
    }
}
