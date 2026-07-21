using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Examine;
using Content.Server.Imperial.Medieval.Language;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.ChargeableAnnounce;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Interaction.Events;
using Content.Shared.Speech;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.Imperial.Medieval.ChargeableAnnounce;

public sealed class ChargeableAnnounceSystem : EntitySystem
{
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ExamineSystem _examine = default!;

    private static readonly Dictionary<string, string> FactionCodeToProto = new()
    {
        ["leg"] = "Legion",
        ["ins"] = "Insurgency",
        ["mine"] = "Miner",
        ["band"] = "Bandit",
        ["kayot"] = "Kayot",
        ["merc"] = "Merc",
        ["collegium"] = "Collegium",
        ["cult"] = "Cult",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChargeableAnnounceComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ChargeableAnnounceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ChargeableAnnounceComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<ChargeableAnnounceComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MedievalFactionMemberComponent, ComponentStartup>(OnFactionMemberStartup);
    }

    private void OnFactionMemberStartup(EntityUid uid, MedievalFactionMemberComponent comp, ComponentStartup args)
    {
        var query = EntityQueryEnumerator<ChargeableAnnounceComponent, CloackRecieverComponent>();
        while (query.MoveNext(out var crystalUid, out var crystal, out var receiver))
        {
            if (crystal.OwnerUid.HasValue)
                continue;

            TryBindFromContainerHierarchy(crystalUid, crystal, receiver.Faction);
        }
    }

    private void OnMapInit(EntityUid uid, ChargeableAnnounceComponent comp, MapInitEvent args)
    {
        if (!TryComp<CloackRecieverComponent>(uid, out var receiver))
            return;

        TryBindFromContainerHierarchy(uid, comp, receiver.Faction);
    }

    private void OnInsertedIntoContainer(EntityUid uid, ChargeableAnnounceComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (comp.OwnerUid.HasValue)
            return;

        if (!TryComp<CloackRecieverComponent>(uid, out var receiver))
            return;

        TryBindFromContainerHierarchy(uid, comp, receiver.Faction, args.Container.Owner);
    }

    private void OnExamined(EntityUid uid, ChargeableAnnounceComponent comp, ExaminedEvent args)
    {
        if (!comp.OwnerUid.HasValue)
        {
            args.PushMarkup("[color=gray]Кристалл бесхозный.[/color]");
            return;
        }

        if (Deleted(comp.OwnerUid.Value))
            return;

        var ownerName = Identity.Name(comp.OwnerUid.Value, EntityManager);
        args.PushMarkup($"[color=gray]Владелец: {ownerName}[/color]");
    }

    private void OnUseInHand(EntityUid uid, ChargeableAnnounceComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<CloackRecieverComponent>(uid, out var receiver))
            return;

        if (!comp.IsCharged)
        {
            _popup.PopupEntity("Кристалл связи разряжен.", uid, args.User);
            return;
        }

        if (!comp.OwnerUid.HasValue)
        {
            _popup.PopupEntity("Кристалл ни к кому не привязан.", uid, args.User);
            return;
        }

        if (comp.OwnerUid.Value != args.User)
        {
            _popup.PopupEntity("Это не ваш кристалл связи.", uid, args.User);
            return;
        }

        if (!_sharedPlayerManager.TryGetSessionByEntity(args.User, out var session))
            return;

        args.Handled = true;

        var sender = args.User;

        _quickDialog.OpenDialog(session, "Весть", "Сообщение", (string message) =>
        {
            if (string.IsNullOrWhiteSpace(message) || Deleted(uid) || !TryComp<ChargeableAnnounceComponent>(uid, out var announce))
                return;

            if (Deleted(sender))
                return;

            var language = _language.GetCurrentLanguage(sender);

            var query = EntityQueryEnumerator<CloackRecieverComponent>();
            while (query.MoveNext(out var cloackOwner, out var cloack))
            {
                if (cloack.Faction != receiver.Faction)
                    continue;

                EnsureComp<SpeechComponent>(cloackOwner);
                SendCommsCrystalWhisper(cloackOwner, sender, message, language);
            }

            announce.IsCharged = false;
            Dirty(uid, announce);
        });
    }

    private void SendCommsCrystalWhisper(
        EntityUid crystal,
        EntityUid sender,
        string originalMessage,
        LanguagePrototype language)
    {
        var message = FormattedMessage.RemoveMarkupOrThrow(originalMessage);
        if (message.Length == 0)
            return;

        foreach (var item in language.Conditions.Where(x => !x.RaiseOnListener))
        {
            if (!item.Condition(sender, null, EntityManager))
                return;
        }

        message = _chat.SanitizeInGameICMessage(sender, message, out _);
        if (string.IsNullOrEmpty(message))
            return;

        if (language.LanguageType is not Generic generic)
        {
            var nameEv = new TransformSpeakerNameEvent(sender, Name(sender));
            RaiseLocalEvent(sender, nameEv);
            _chat.TrySendInGameICMessage(
                crystal,
                originalMessage,
                InGameICChatType.Whisper,
                hideChat: false,
                nameOverride: nameEv.VoiceName,
                ignoreActionBlocker: true,
                language: language);
            return;
        }

        message = _chat.TransformSpeech(sender, message);

        var accentMessage = _language.AccentuateMessage(sender, language.ID, message);
        var languageMessage = _language.ObfuscateMessage(sender, message, generic.Replacement, generic.ObfuscateSyllables);
        var obfuscatedMessage = _chat.ObfuscateMessageReadability(accentMessage, 0.2f);
        var obfuscatedLanguageMessage = _chat.ObfuscateMessageReadability(languageMessage, 0.2f);

        var langType = language.LanguageType;
        if (langType.WhisperColor != null)
        {
            var color = langType.WhisperColor.Value.ToHex();
            accentMessage = $"[color={color}]{accentMessage}[/color]";
            languageMessage = $"[color={color}]{languageMessage}[/color]";
            obfuscatedMessage = $"[color={color}]{obfuscatedMessage}[/color]";
            obfuscatedLanguageMessage = $"[color={color}]{obfuscatedLanguageMessage}[/color]";
        }

        var font = langType.Font;
        var fontSize = langType.FontSize;

        foreach (var (session, data) in _chat.GetWhisperRecipients(crystal, ChatSystem.WhisperClearRange, ChatSystem.WhisperMuffledRange))
        {
            if (session.AttachedEntity is not { Valid: true } listener)
                continue;

            var listenerCondition = true;
            foreach (var item in language.Conditions.Where(x => x.RaiseOnListener))
            {
                if (!item.Condition(listener, crystal, EntityManager))
                    listenerCondition = false;
            }

            if (!listenerCondition)
                continue;

            if (_chat.MessageRangeCheck(session, data, ChatTransmitRange.Normal) != ChatSystem.MessageRangeCheckResult.Full)
                continue;

            var understands = _language.CanUnderstand(listener, language);
            var entityName = FormattedMessage.EscapeText(Identity.Name(sender, EntityManager, listener, true));

            if (data.Range <= ChatSystem.WhisperClearRange || data.Observer)
            {
                var wrappedMessage = Loc.GetString("chat-manager-entity-lang-whisper-wrap-message",
                    ("entityName", entityName),
                    ("fontType", font ?? "NotoSansDisplayItalic"),
                    ("fontSize", fontSize ?? 11),
                    ("defaultFont", "NotoSansDisplayItalic"),
                    ("defaultSize", 11),
                    ("message", understands ? accentMessage : languageMessage));

                _chatManager.ChatMessageToOne(ChatChannel.Whisper, message, wrappedMessage, crystal, false, session.Channel);
            }
            else if (_examine.InRangeUnOccluded(crystal, listener, ChatSystem.WhisperMuffledRange))
            {
                var wrappedMessage = Loc.GetString("chat-manager-entity-lang-whisper-wrap-message",
                    ("entityName", entityName),
                    ("fontType", font ?? "NotoSansDisplayItalic"),
                    ("fontSize", fontSize ?? 11),
                    ("defaultFont", "NotoSansDisplayItalic"),
                    ("defaultSize", 11),
                    ("message", understands ? obfuscatedMessage : obfuscatedLanguageMessage));

                _chatManager.ChatMessageToOne(ChatChannel.Whisper, obfuscatedMessage, wrappedMessage, crystal, false, session.Channel);
            }
            else
            {
                var wrappedMessage = Loc.GetString("chat-manager-entity-lang-whisper-unknown-wrap-message",
                    ("fontType", font ?? "NotoSansDisplayItalic"),
                    ("fontSize", fontSize ?? 11),
                    ("defaultFont", "NotoSansDisplayItalic"),
                    ("defaultSize", 11),
                    ("message", understands ? obfuscatedMessage : obfuscatedLanguageMessage));

                _chatManager.ChatMessageToOne(ChatChannel.Whisper, obfuscatedMessage, wrappedMessage, crystal, false, session.Channel);
            }
        }

        if (langType.RaiseEvent)
        {
            var ev = new EntitySpokeEvent(
                crystal,
                FormattedMessage.EscapeText(accentMessage),
                language,
                null,
                FormattedMessage.EscapeText(obfuscatedMessage),
                true);
            RaiseLocalEvent(crystal, ev, true);
        }
    }

    private void TryBindFromContainerHierarchy(EntityUid crystalUid, ChargeableAnnounceComponent comp, string crystalFaction, EntityUid? startEntity = null)
    {
        EntityUid current;

        if (startEntity.HasValue)
        {
            current = startEntity.Value;
        }
        else
        {
            if (!_container.TryGetContainingContainer(crystalUid, out var rootContainer))
                return;
            current = rootContainer.Owner;
        }

        while (true)
        {
            if (TryComp<MedievalFactionMemberComponent>(current, out var factionMember))
            {
                if (FactionCodeToProto.TryGetValue(crystalFaction, out var protoId)
                    && factionMember.Faction == protoId)
                {
                    comp.OwnerUid = current;
                    Dirty(crystalUid, comp);
                }
                return;
            }

            if (!_container.TryGetContainingContainer(current, out var parentContainer))
                return;

            current = parentContainer.Owner;
        }
    }
}
