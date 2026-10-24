using System.Linq;
using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Uplink.Stealth;
using Content.Shared.Chat;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Flash;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Antimagic;
using Content.Shared.Imperial.Chaplain.Components;
using Content.Shared.Imperial.Uplink.Stealth;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Uplink.Stealth;

/// <summary>
/// Скрытное оружие и гаджеты аплинка TG: помада, глушитель, оригами, острая лента, линзы, том Синдиката, кодовая речь
/// и остальные гаджеты (см. partial-файлы).
/// </summary>
public sealed partial class UplinkStealthSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly QuickDialogSystem _dialog = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly Robust.Server.GameObjects.UserInterfaceSystem _launchpadUi = default!;
    [Dependency] private readonly Content.Shared.Weapons.Ranged.Systems.SharedGunSystem _gun = default!;

    private static readonly SoundSpecifier SnapSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");
    private static readonly SoundSpecifier ScrewSound = new SoundPathSpecifier("/Audio/Items/screwdriver.ogg");
    private static readonly SoundSpecifier SuppressedSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/shot_suppressed.ogg", AudioParams.Default.WithVolume(-6f).WithMaxDistance(3f));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UplinkSyndieLipstickComponent, UseInHandEvent>(OnLipstickUse);
        SubscribeLocalEvent<UplinkSyndieKisserComponent, EmoteEvent>(OnKisserEmote);
        SubscribeLocalEvent<UplinkSingleShotComponent, AmmoShotEvent>(OnSingleShot);

        SubscribeLocalEvent<UplinkSuppressibleComponent, InteractUsingEvent>(OnSuppressibleInteract);
        SubscribeLocalEvent<UplinkSuppressibleComponent, GetVerbsEvent<AlternativeVerb>>(OnSuppressibleVerbs);
        SubscribeLocalEvent<UplinkSuppressibleComponent, ExaminedEvent>(OnSuppressibleExamined);
        SubscribeLocalEvent<UplinkSuppressibleComponent, GunRefreshModifiersEvent>(OnSuppressibleRefresh);
        SubscribeLocalEvent<UplinkPointyTapedComponent, ThrowDoHitEvent>(OnPointyTapedHit);

        SubscribeLocalEvent<UplinkOrigamiBookComponent, UseInHandEvent>(OnOrigamiBookUse);
        SubscribeLocalEvent<PaperComponent, GetVerbsEvent<AlternativeVerb>>(OnPaperVerbs);
        SubscribeLocalEvent<UplinkPaperPlaneComponent, ThrowDoHitEvent>(OnPaperPlaneHit);

        SubscribeLocalEvent<UplinkPointyTapeComponent, AfterInteractEvent>(OnPointyTapeInteract);

        SubscribeLocalEvent<UplinkSyndicateContactsComponent, UseInHandEvent>(OnContactsUse);
        SubscribeLocalEvent<UplinkSyndicateContactsComponent, UplinkContactsDoAfterEvent>(OnContactsDoAfter);
        SubscribeLocalEvent<UplinkFlashProtectedComponent, FlashAttemptEvent>(OnFlashAttempt);

        SubscribeLocalEvent<UplinkSyndieTomeComponent, UseInHandEvent>(OnTomeUse);
        SubscribeLocalEvent<UplinkSyndieTomeComponent, ExaminedEvent>(OnTomeExamined);

        SubscribeLocalEvent<UplinkCodespeakManualComponent, UseInHandEvent>(OnCodespeakUse);
        SubscribeLocalEvent<UplinkCodespeakManualComponent, AfterInteractEvent>(OnCodespeakAfterInteract);
        SubscribeLocalEvent<UplinkCodespeakerComponent, TransformSpeechEvent>(OnCodespeakSpeech, after: [typeof(ChatSystem)]);

        InitializeGadgets();
        InitializeSabotage();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateGadgets();
        UpdateSabotage();
    }

    #region Помада Синдиката

    private void OnLipstickUse(Entity<UplinkSyndieLipstickComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var kisser = EnsureComp<UplinkSyndieKisserComponent>(args.User);
        kisser.Kisser = ent.Comp.Kisser;
        _popup.PopupEntity(Loc.GetString("uplink-lipstick-apply"), args.User, args.User);
    }

    /// <summary>/datum/emote/living/kiss: в руку кладётся «воздушный поцелуй», который стреляет один раз.</summary>
    private void OnKisserEmote(Entity<UplinkSyndieKisserComponent> ent, ref EmoteEvent args)
    {
        if (args.Emote.ID != "ImperialKiss")
            return;

        var kiss = Spawn(ent.Comp.Kisser, Transform(ent).Coordinates);
        if (!_hands.TryPickupAnyHand(ent, kiss))
        {
            QueueDel(kiss);
            _popup.PopupEntity(Loc.GetString("uplink-kiss-no-hand"), ent, ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("uplink-kiss-ready"), ent, ent);
    }

    private void OnSingleShot(Entity<UplinkSingleShotComponent> ent, ref AmmoShotEvent args)
    {
        QueueDel(ent);
    }

    #endregion

    #region Глушитель

    private void OnSuppressibleExamined(Entity<UplinkSuppressibleComponent> ent, ref ExaminedEvent args)
    {
        if (IsSuppressed(ent))
            args.PushMarkup(Loc.GetString("uplink-suppressor-examine-on"));
        else
            args.PushMarkup(Loc.GetString("uplink-suppressor-examine-off"));
    }

    private bool IsSuppressed(Entity<UplinkSuppressibleComponent> ent)
    {
        return _container.TryGetContainer(ent, ent.Comp.Slot, out var container) && container.ContainedEntities.Count > 0;
    }

    private void OnSuppressibleInteract(Entity<UplinkSuppressibleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<UplinkSuppressorComponent>(args.Used))
            return;

        args.Handled = true;
        if (IsSuppressed(ent))
        {
            _popup.PopupEntity(Loc.GetString("uplink-suppressor-already"), ent, args.User);
            return;
        }

        var container = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Slot);
        if (!_container.Insert(args.Used, container))
            return;

        SetSuppressed(ent, true);
        _audio.PlayPvs(ScrewSound, ent);
        _popup.PopupEntity(Loc.GetString("uplink-suppressor-attach"), ent, args.User);
    }

    private void OnSuppressibleVerbs(Entity<UplinkSuppressibleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !IsSuppressed(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("uplink-suppressor-remove-verb"),
            Act = () =>
            {
                if (!_container.TryGetContainer(ent, ent.Comp.Slot, out var container))
                    return;

                foreach (var item in container.ContainedEntities.ToArray())
                {
                    _container.Remove(item, container);
                    _hands.PickupOrDrop(user, item);
                }

                SetSuppressed(ent, false);
                _audio.PlayPvs(ScrewSound, ent);
            },
        });
    }

    /// <summary>SUPPRESSED_QUIET: звук выстрела тише и глуше.</summary>
    private void SetSuppressed(Entity<UplinkSuppressibleComponent> ent, bool suppressed)
    {
        _appearance.SetData(ent, UplinkSuppressorVisuals.Suppressed, suppressed);
        _gun.RefreshModifiers(ent.Owner);
    }

    private void OnSuppressibleRefresh(Entity<UplinkSuppressibleComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (IsSuppressed(ent))
            args.SoundGunshot = SuppressedSound;
    }

    #endregion

    #region Оригами

    private void OnOrigamiBookUse(Entity<UplinkOrigamiBookComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (HasComp<UplinkPaperMasterComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("uplink-origami-already"), args.User, args.User);
            return;
        }

        EnsureComp<UplinkPaperMasterComponent>(args.User);
        _popup.PopupEntity(Loc.GetString("uplink-origami-learn"), args.User, args.User, PopupType.Medium);
    }

    /// <summary>paper/click_alt: мастер оригами складывает боевой самолётик.</summary>
    private void OnPaperVerbs(Entity<PaperComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<UplinkPaperMasterComponent>(args.User) || HasComp<UplinkPaperPlaneComponent>(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("uplink-origami-fold-verb"),
            Priority = 5,
            Act = () =>
            {
                var coords = Transform(ent).Coordinates;
                QueueDel(ent);
                var plane = Spawn("ImperialPaperPlaneSyndicate", coords);
                _hands.PickupOrDrop(user, plane);
                _popup.PopupEntity(Loc.GetString("uplink-origami-folded"), user, user);
            },
        });
    }

    /// <summary>paperplane/throw_impact: попадание в незащищённые глаза.</summary>
    private void OnPaperPlaneHit(Entity<UplinkPaperPlaneComponent> ent, ref ThrowDoHitEvent args)
    {
        var target = args.Target;
        if (!HasComp<MobStateComponent>(target) || !_random.Prob(ent.Comp.HitProbability))
            return;

        // is_eyes_covered: очки, маска или шлем закрывают глаза.
        if (_inventory.TryGetSlotEntity(target, "eyes", out _) || _inventory.TryGetSlotEntity(target, "mask", out _))
            return;

        _popup.PopupEntity(Loc.GetString("uplink-paperplane-eye", ("target", target)), target, PopupType.MediumCaution);
        _status.TryAddStatusEffectDuration(target, "StatusEffectBlurryVision", TimeSpan.FromSeconds(12));
        _stun.TryAddParalyzeDuration(target, TimeSpan.FromSeconds(4));
        _chat.TryEmoteWithChat(target, "Scream");
    }

    #endregion

    #region Острая лента

    private void OnPointyTapeInteract(Entity<UplinkPointyTapeComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!HasComp<Content.Shared.Item.ItemComponent>(target) || HasComp<UplinkPointyTapedComponent>(target) || target == ent.Owner)
            return;

        args.Handled = true;
        if (!_stack.TryUse(ent.Owner, 1))
            return;

        EnsureComp<UplinkPointyTapedComponent>(target).Damage = ent.Comp.Damage;
        var embed = EnsureComp<EmbeddableProjectileComponent>(target);
        embed.RemovalTime = ent.Comp.RemovalTime;
        Dirty(target, embed);

        _popup.PopupEntity(Loc.GetString("uplink-pointy-tape-apply", ("target", target)), args.User, args.User);
    }

    /// <summary>conferred_embed: брошенный предмет вонзается и ранит.</summary>
    private void OnPointyTapedHit(Entity<UplinkPointyTapedComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Target) || ent.Comp.Damage <= 0)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict["Piercing"] = ent.Comp.Damage;
        _damageable.TryChangeDamage(args.Target, damage, origin: args.Component.Thrower);
    }

    #endregion

    #region Контактные линзы

    private void OnContactsUse(Entity<UplinkSyndicateContactsComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("uplink-contacts-start"), args.User, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3), new UplinkContactsDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnContactsDoAfter(Entity<UplinkSyndicateContactsComponent> ent, ref UplinkContactsDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        EnsureComp<UplinkFlashProtectedComponent>(args.User);
        _popup.PopupEntity(Loc.GetString("uplink-contacts-done"), args.User, args.User);
        QueueDel(ent);
    }

    private void OnFlashAttempt(Entity<UplinkFlashProtectedComponent> ent, ref FlashAttemptEvent args)
    {
        args.Cancelled = true;
    }

    #endregion

    #region Том Синдиката

    private void OnTomeExamined(Entity<UplinkSyndieTomeComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.OwnerName != null)
            args.PushMarkup(Loc.GetString("uplink-tome-owner", ("name", ent.Comp.OwnerName)));
    }

    /// <summary>bible/syndicate/attack_self: даёт святую роль жреца, кусает руку и подписывается кровью.</summary>
    private void OnTomeUse(Entity<UplinkSyndieTomeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Uses <= 0)
            return;

        args.Handled = true;
        ent.Comp.Uses--;

        var holy = EnsureComp<ImperialHolyComponent>(args.User);
        if (holy.Role < HolyRole.Priest)
            holy.Role = HolyRole.Priest;
        Dirty(args.User, holy);
        EnsureComp<Content.Server.Bible.Components.BibleUserComponent>(args.User);

        _audio.PlayPvs(SnapSound, ent);
        var bite = new DamageSpecifier();
        bite.DamageDict["Piercing"] = 5;
        _damageable.TryChangeDamage(args.User, bite, true);
        _popup.PopupEntity(Loc.GetString("uplink-tome-bite"), args.User, args.User, PopupType.LargeCaution);

        ent.Comp.OwnerName = Name(args.User);
    }

    #endregion

    #region Кодовая речь

    private void OnCodespeakUse(Entity<UplinkCodespeakManualComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TeachCodespeak(ent, args.User, args.User);
    }

    /// <summary>Ударом мануала можно научить другого.</summary>
    private void OnCodespeakAfterInteract(Entity<UplinkCodespeakManualComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;
        TeachCodespeak(ent, args.User, target);
    }

    private void TeachCodespeak(Entity<UplinkCodespeakManualComponent> ent, EntityUid user, EntityUid target)
    {
        if (HasComp<UplinkCodespeakerComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("uplink-codespeak-already"), user, user);
            return;
        }

        if (ent.Comp.Charges == 0)
        {
            _popup.PopupEntity(Loc.GetString("uplink-codespeak-empty"), user, user);
            return;
        }

        if (ent.Comp.Charges > 0)
            ent.Comp.Charges--;

        EnsureComp<UplinkCodespeakerComponent>(target);
        _popup.PopupEntity(Loc.GetString("uplink-codespeak-learn"), target, target, PopupType.Medium);
    }

    /// <summary>
    /// Кодовая речь (language/codespeak): фраза с префиксом «%» для остальных звучит как набор кодовых слов,
    /// а знающие кодовую речь получают её смысл.
    /// </summary>
    private void OnCodespeakSpeech(Entity<UplinkCodespeakerComponent> ent, ref TransformSpeechEvent args)
    {
        var message = args.Message.TrimStart();
        if (!message.StartsWith('%'))
            return;

        var original = message[1..].Trim();
        if (original.Length == 0)
            return;

        args.Message = GenerateCodePhrase(original.Length);

        var name = Name(ent);
        var wrapped = Loc.GetString("uplink-codespeak-wrap", ("name", name), ("message", FormattedMessageEscape(original)));
        var coords = _transform.GetMapCoordinates(ent);
        foreach (var listener in _lookup.GetEntitiesInRange<UplinkCodespeakerComponent>(coords, 10f))
        {
            if (!_player.TryGetSessionByEntity(listener, out var session))
                continue;

            _chatManager.ChatMessageToOne(ChatChannel.Local, original, wrapped, ent, false, session.Channel);
        }
    }

    private static string FormattedMessageEscape(string text)
    {
        return Robust.Shared.Utility.FormattedMessage.EscapeText(text);
    }

    private string GenerateCodePhrase(int length)
    {
        var words = Loc.GetString("uplink-codespeak-words").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var count = Math.Clamp(length / 6 + 1, 2, 8);
        var result = new List<string>();
        for (var i = 0; i < count; i++)
        {
            result.Add(_random.Pick(words));
        }

        var phrase = string.Join(", ", result);
        return char.ToUpper(phrase[0]) + phrase[1..] + ".";
    }

    #endregion
}
