using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Languages;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Speech;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Languages;

/// <summary>
/// Языки TG: речь на чужом языке перемешивается для тех, кто его не понимает (scramble_paragraph).
/// Ванильный чат не трогается: при разговоре на языке получатели забираются из рассылки и получают свою версию.
/// </summary>
public sealed partial class ImperialLanguageSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedImperialLanguageSystem _lang = default!;

    private static readonly EntProtoId MenuAction = "ImperialActionLanguageMenu";
    private static readonly ProtoId<ImperialLanguageSetPrototype> DefaultSet = "Default";
    private static readonly ProtoId<ImperialLanguageSetPrototype> BorgSet = "Borg";
    private static readonly ProtoId<ImperialLanguageSetPrototype> AiSet = "StationAi";

    /// <summary>Речь в процессе: язык фразы и слушатели, которых забрали из ванильной рассылки.</summary>
    private sealed class Utterance
    {
        public ProtoId<ImperialLanguagePrototype> Language;
        public GameTick Tick;
        public float VoiceRange;
        public readonly List<(ICommonSession Session, float Range)> Listeners = new();
    }

    private readonly Dictionary<EntityUid, Utterance> _utterances = new();

    /// <summary>Язык сообщения, которое сейчас повторяет динамик рации (интерком, рация в руке).</summary>
    private (ProtoId<ImperialLanguagePrototype> Language, GameTick Tick)? _relay;

    /// <summary>Исходное сообщение рации до подмены для очередного приёмника.</summary>
    private readonly Dictionary<MsgChatMessage, (ChatMessage Original, string Text, ProtoId<ImperialLanguagePrototype> Language)> _radio = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech, before: [typeof(Content.Server.Speech.AccentSystem)]);
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
        SubscribeLocalEvent<EntitySpokeEvent>(OnSpoke);
        SubscribeLocalEvent<ActiveRadioComponent, RadioReceiveEvent>(OnRadioReceive,
            before: [typeof(HeadsetSystem), typeof(RadioDeviceSystem), typeof(RadioSystem)]);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ImperialLanguageSpeakerComponent, MapInitEvent>(OnSpeakerInit);
        SubscribeLocalEvent<ImperialLanguageSpeakerComponent, ImperialLanguageMenuActionEvent>(OnMenuAction);
        SubscribeNetworkEvent<ImperialLanguageSelectMessage>(OnSelect);
        SubscribeLocalEvent<ImperialQuirkLanguageComponent, ComponentStartup>(OnLanguageQuirk);
    }

    #region Языки существа

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args) => EnsureLanguages(args.Mob);

    private void OnPlayerAttached(PlayerAttachedEvent args) => EnsureLanguages(args.Entity);

    /// <summary>Выдаёт набор языков по виду, если у существа его ещё нет. Без набора существо знает только общий.</summary>
    public void EnsureLanguages(EntityUid uid)
    {
        if (HasComp<ImperialLanguageSpeakerComponent>(uid))
            return;

        ProtoId<ImperialLanguageSetPrototype>? setId = null;
        if (TryComp<HumanoidProfileComponent>(uid, out var profile))
            setId = _proto.HasIndex<ImperialLanguageSetPrototype>(profile.Species.Id) ? profile.Species.Id : DefaultSet;
        else if (HasComp<BorgChassisComponent>(uid))
            setId = BorgSet;
        else if (HasComp<StationAiHeldComponent>(uid))
            setId = AiSet;

        if (setId is not { } id || !_proto.TryIndex(id, out var set))
            return;

        var speaker = AddComp<ImperialLanguageSpeakerComponent>(uid);
        speaker.Spoken.AddRange(set.Spoken);
        speaker.Understood.AddRange(set.Understood);
        foreach (var (language, percent) in set.Partial)
            speaker.Partial[language] = percent;
        Dirty(uid, speaker);
        GrantMenu((uid, speaker));
    }

    private void OnSpeakerInit(Entity<ImperialLanguageSpeakerComponent> ent, ref MapInitEvent args) => GrantMenu(ent);

    private void GrantMenu(Entity<ImperialLanguageSpeakerComponent> ent)
    {
        if (ent.Comp.MenuAction == null)
            _actions.AddAction(ent, ref ent.Comp.MenuAction, MenuAction);
    }

    /// <summary>grant_language.</summary>
    public void GrantLanguage(EntityUid uid, ProtoId<ImperialLanguagePrototype> language, bool spoken = true, bool understood = true)
    {
        EnsureLanguages(uid);
        var speaker = EnsureComp<ImperialLanguageSpeakerComponent>(uid);
        if (spoken && !speaker.Spoken.Contains(language))
            speaker.Spoken.Add(language);
        if (understood && !speaker.Understood.Contains(language))
            speaker.Understood.Add(language);
        Dirty(uid, speaker);
        GrantMenu((uid, speaker));
    }

    /// <summary>remove_language.</summary>
    public void RemoveLanguage(EntityUid uid, ProtoId<ImperialLanguagePrototype> language)
    {
        if (!TryComp<ImperialLanguageSpeakerComponent>(uid, out var speaker))
            return;

        speaker.Spoken.Remove(language);
        speaker.Understood.Remove(language);
        if (speaker.Current == language)
            speaker.Current = null;
        Dirty(uid, speaker);
    }

    /// <summary>grant_partial_language.</summary>
    public void GrantPartial(EntityUid uid, ProtoId<ImperialLanguagePrototype> language, int percent)
    {
        EnsureLanguages(uid);
        var speaker = EnsureComp<ImperialLanguageSpeakerComponent>(uid);
        speaker.Partial[language] = percent;
        Dirty(uid, speaker);
    }

    /// <summary>add_blocked_language / remove_blocked_language.</summary>
    public void SetBlocked(EntityUid uid, ProtoId<ImperialLanguagePrototype> language, bool blocked)
    {
        EnsureLanguages(uid);
        var speaker = EnsureComp<ImperialLanguageSpeakerComponent>(uid);
        if (blocked && !speaker.Blocked.Contains(language))
            speaker.Blocked.Add(language);
        else if (!blocked)
            speaker.Blocked.Remove(language);
        Dirty(uid, speaker);
    }

    private void OnLanguageQuirk(Entity<ImperialQuirkLanguageComponent> ent, ref ComponentStartup args)
    {
        EnsureLanguages(ent);
        var speaker = EnsureComp<ImperialLanguageSpeakerComponent>(ent);

        var options = ent.Comp.GrantRandom.Where(l => !speaker.Spoken.Contains(l)).ToList();
        if (options.Count > 0)
            GrantLanguage(ent, _random.Pick(options));
        else if (ent.Comp.GrantRandom.Count > 0)
            GrantLanguage(ent, ent.Comp.GrantRandom[0]);

        foreach (var language in ent.Comp.Grant)
            GrantLanguage(ent, language);

        foreach (var language in ent.Comp.Block)
            SetBlocked(ent, language, true);

        foreach (var (language, percent) in ent.Comp.Partial)
        {
            speaker.Understood.Remove(language);
            GrantPartial(ent, language, percent);
        }

        // Говорить на выученном языке по умолчанию, если общий недоступен.
        speaker.Current = _lang.CanSpeak((ent, speaker), ImperialLanguages.Common) ? speaker.Current : null;
        Dirty(ent, speaker);
    }

    private void OnMenuAction(Entity<ImperialLanguageSpeakerComponent> ent, ref ImperialLanguageMenuActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        RaiseNetworkEvent(new ImperialLanguageMenuOpenMessage(), actor.PlayerSession);
    }

    private void OnSelect(ImperialLanguageSelectMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid
            || !TryComp<ImperialLanguageSpeakerComponent>(uid, out var speaker)
            || !_lang.CanSpeak((uid, speaker), msg.Language))
        {
            return;
        }

        speaker.Current = msg.Language;
        Dirty(uid, speaker);
    }

    #endregion

    #region Речь

    /// <summary>«,ключ» в начале фразы выбирает язык; иначе — выбранный по умолчанию.</summary>
    private void OnTransformSpeech(TransformSpeechEvent args)
    {
        var source = args.Sender;
        var tick = _timing.CurTick;
        EnsureLanguages(source);

        ProtoId<ImperialLanguagePrototype> language;
        if (_relay is { } relay && relay.Tick == tick && HasComp<RadioSpeakerComponent>(source))
        {
            language = relay.Language;
        }
        else
        {
            language = _lang.GetCurrent(source);
            var message = args.Message;
            if (message.Length >= 2 && message[0] == ',')
            {
                var key = char.ToLowerInvariant(message[1]).ToString();
                var chosen = _proto.EnumeratePrototypes<ImperialLanguagePrototype>().FirstOrDefault(l => l.Key == key);
                if (chosen != null && _lang.CanSpeak(source, chosen.ID))
                {
                    language = chosen.ID;
                    args.Message = message[2..].TrimStart();
                }
            }
        }

        _utterances[source] = new Utterance { Language = language, Tick = tick };
    }

    /// <summary>Слушателей, которым нужна своя версия фразы, забираем из ванильной рассылки.</summary>
    private void OnExpandRecipients(ExpandICChatRecipientsEvent args)
    {
        if (!_utterances.TryGetValue(args.Source, out var utterance) || utterance.Tick != _timing.CurTick)
            return;

        utterance.VoiceRange = args.VoiceRange;
        utterance.Listeners.Clear();

        var custom = utterance.Language != ImperialLanguages.Common;
        foreach (var (session, data) in args.Recipients)
        {
            if (data.Observer || session.AttachedEntity is not { } listener)
                continue;

            if (custom || _lang.Understanding(listener, utterance.Language) < 100)
            {
                custom = true;
                break;
            }
        }

        if (!custom)
            return;

        foreach (var (session, data) in args.Recipients.ToList())
        {
            if (data.Observer || session.AttachedEntity == null)
                continue;

            utterance.Listeners.Add((session, data.Range));
            args.Recipients.Remove(session);
        }
    }

    private void OnSpoke(EntitySpokeEvent args)
    {
        if (!_utterances.Remove(args.Source, out var utterance) || utterance.Tick != _timing.CurTick || utterance.Listeners.Count == 0)
            return;

        if (!_proto.TryIndex(utterance.Language, out var language))
            return;

        var whisper = utterance.VoiceRange < SharedChatSystem.VoiceRange;
        var source = args.Source;
        var nameEv = new TransformSpeakerNameEvent(source, Name(source));
        RaiseLocalEvent(source, nameEv);
        var name = FormattedMessage.EscapeText(nameEv.VoiceName);
        var speech = nameEv.SpeechVerb != null && _proto.TryIndex(nameEv.SpeechVerb, out var verbProto)
            ? verbProto
            : _chat.GetSpeechVerb(source, args.Message);

        foreach (var (session, range) in utterance.Listeners)
        {
            if (session.AttachedEntity is not { } listener || IsDeaf(listener))
                continue;

            var understanding = _lang.Understanding(listener, language.ID);
            var text = understanding >= 100 ? args.Message : ScrambleParagraph(language, args.Message, understanding);
            var shown = Tag(language, understanding >= 100) + FormattedMessage.EscapeText(text);

            if (!whisper)
            {
                var wrapped = Loc.GetString(speech.Bold ? "chat-manager-entity-say-bold-wrap-message" : "chat-manager-entity-say-wrap-message",
                    ("entityName", name),
                    ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
                    ("fontType", speech.FontId),
                    ("fontSize", speech.FontSize),
                    ("message", shown));
                _chatManager.ChatMessageToOne(ChatChannel.Local, text, wrapped, source, false, session.Channel);
                continue;
            }

            if (range <= SharedChatSystem.WhisperClearRange)
            {
                var wrapped = Loc.GetString("chat-manager-entity-whisper-wrap-message", ("entityName", name), ("message", shown));
                _chatManager.ChatMessageToOne(ChatChannel.Whisper, text, wrapped, source, false, session.Channel);
                continue;
            }

            var muffled = Obfuscate(text, 0.2f);
            var muffledShown = FormattedMessage.EscapeText(muffled);
            var visible = _examine.InRangeUnOccluded(source, listener, SharedChatSystem.WhisperMuffledRange);
            var muffledWrapped = visible
                ? Loc.GetString("chat-manager-entity-whisper-wrap-message",
                    ("entityName", FormattedMessage.EscapeText(Content.Shared.IdentityManagement.Identity.Name(source, EntityManager))),
                    ("message", muffledShown))
                : Loc.GetString("chat-manager-entity-whisper-unknown-wrap-message", ("message", muffledShown));
            _chatManager.ChatMessageToOne(ChatChannel.Whisper, muffled, muffledWrapped, source, false, session.Channel);
        }
    }

    /// <summary>Глухие (причуда «Глухота» и т. п.) речь вокруг не слышат.</summary>
    private bool IsDeaf(EntityUid listener)
    {
        return EntityManager.ComponentFactory.TryGetRegistration("ImperialQuirkDeaf", out var deaf) && HasComp(listener, deaf.Type);
    }

    /// <summary>Значок языка из TG: метка перед фразой на нестандартном языке.</summary>
    private string Tag(ImperialLanguagePrototype language, bool understood)
    {
        if (language.HideIconIfUnderstood && understood)
            return string.Empty;

        return $"[color={language.Color.ToHex()}]({Loc.GetString(language.Name)})[/color] ";
    }

    private string Obfuscate(string message, float chance)
    {
        var sb = new StringBuilder(message);
        for (var i = 0; i < sb.Length; i++)
        {
            if (!char.IsWhiteSpace(sb[i]) && _random.Prob(1 - chance))
                sb[i] = '~';
        }

        return sb.ToString();
    }

    #endregion

    #region Рация

    /// <summary>
    /// Каждый приёмник получает свою версию: гарнитура — по языкам носителя, динамик — исходную,
    /// а затем сам повторяет её вслух на том же языке.
    /// </summary>
    private void OnRadioReceive(Entity<ActiveRadioComponent> ent, ref RadioReceiveEvent args)
    {
        var tick = _timing.CurTick;
        if (!_radio.TryGetValue(args.ChatMsg, out var info))
        {
            var language = _utterances.TryGetValue(args.MessageSource, out var utterance) && utterance.Tick == tick
                ? utterance.Language
                : _lang.GetCurrent(args.MessageSource);

            info = (args.ChatMsg.Message, args.Message, language);
            if (_radio.Count > 64)
                _radio.Clear();
            _radio[args.ChatMsg] = info;
        }

        var original = info.Original;
        if (HasComp<RadioSpeakerComponent>(ent))
        {
            _relay = (info.Language, tick);
            args.ChatMsg.Message = original;
            return;
        }

        var listener = HasComp<IntrinsicRadioReceiverComponent>(ent) ? ent.Owner : Transform(ent).ParentUid;
        if (!_proto.TryIndex(info.Language, out var proto) || !listener.IsValid())
        {
            args.ChatMsg.Message = original;
            return;
        }

        var understanding = _lang.Understanding(listener, info.Language);
        if (understanding >= 100 && proto.HideIconIfUnderstood)
        {
            args.ChatMsg.Message = original;
            return;
        }

        var text = understanding >= 100 ? info.Text : ScrambleParagraph(proto, info.Text, understanding);
        var wrapped = original.WrappedMessage.Replace(FormattedMessage.EscapeText(info.Text), Tag(proto, understanding >= 100) + FormattedMessage.EscapeText(text));
        args.ChatMsg.Message = new ChatMessage(original.Channel, text, wrapped, original.SenderEntity, original.SenderKey,
            original.HideChat, original.MessageColorOverride, original.AudioPath, original.AudioVolume);
    }

    #endregion
}

/// <summary>
/// Языковые причуды TG: bilingual (ещё один язык), foreigner (не знает общего), csl (общий понимает частично).
/// Добавляется причудой; применяется один раз при появлении компонента.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialQuirkLanguageComponent : Component
{
    /// <summary>Выдать один случайный язык из списка, которого ещё нет (bilingual).</summary>
    [DataField]
    public List<ProtoId<ImperialLanguagePrototype>> GrantRandom = new();

    [DataField]
    public List<ProtoId<ImperialLanguagePrototype>> Grant = new();

    [DataField]
    public List<ProtoId<ImperialLanguagePrototype>> Block = new();

    /// <summary>Язык перестаёт пониматься полностью, остаётся указанный процент.</summary>
    [DataField]
    public Dictionary<ProtoId<ImperialLanguagePrototype>, int> Partial = new();
}
