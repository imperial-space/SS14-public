using Content.Server.Actions;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Flash;
using Content.Server.Imperial.Medieval.Administration.Nrp;
using Content.Server.Popups;
using Content.Shared.Alert;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Imperial.Medieval.Illitid;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Illitid;

// TODO: нормальная локализация
public sealed class IllitidSystem : SharedIllitidSystem
{

    #region Dependencies
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffectsSystem = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly NrpMessagesSystem _nrpMessages = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly FlashSystem _flash = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    #endregion

    #region Const
    [ValidatePrototypeId<EntityPrototype>] private const string ThoughtActionId     = "MedievalActionIllitidThought";
    [ValidatePrototypeId<EntityPrototype>] private const string MassThoughtActionId = "MedievalActionIllitidMassThought";
    [ValidatePrototypeId<EntityPrototype>] private const string ForceTalkActionId   = "MedievalActionIllitidForceTalk";
    [ValidatePrototypeId<EntityPrototype>] private const string BlindnessActionId   = "MedievalActionIllitidBlindness";

    private const string ThoughtCast = "/Audio/Imperial/Medieval/Illitid/cast1.ogg";
    private const string ThoughtReceive = "/Audio/Imperial/Medieval/Illitid/target1.ogg";

    private const string ForceSayCast = "/Audio/Imperial/Medieval/Illitid/cast2.ogg";
    private const string ForceSayReceive = "/Audio/Imperial/Medieval/Illitid/target2.ogg";

    private const string BlindnessCast = "/Audio/Imperial/Medieval/Illitid/cast3.ogg";
    private const string BlindnessReceive = "/Audio/Imperial/Medieval/Illitid/target3.ogg";
    #endregion

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IllitidComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<IllitidComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<IllitidComponent, IllitidThoughtActionEvent>(OnThoughtAction);
        SubscribeLocalEvent<IllitidComponent, IllitidMassThoughtActionEvent>(OnMassThoughtAction);
        SubscribeLocalEvent<IllitidComponent, IllitidForceSayActionEvent>(OnForceSayAction);
        SubscribeLocalEvent<IllitidComponent, IllitidBlindnessActionEvent>(OnBlindnessAction);
    }

    private void OnMapInit(EntityUid uid, IllitidComponent component, MapInitEvent args)
    {
        _action.AddAction(uid, ref component.ThoughtAction, ThoughtActionId);
        _action.AddAction(uid, ref component.MassThoughtAction, MassThoughtActionId);
        _action.AddAction(uid, ref component.ForceTalkAction, ForceTalkActionId);
        _action.AddAction(uid, ref component.BlindnessAction, BlindnessActionId);
    }

    private void OnStartup(EntityUid uid, IllitidComponent component, ComponentStartup args)
    {
        ChangePsiAmount(uid, 0, component);
    }

    #region Action events
    private void OnThoughtAction(EntityUid uid, IllitidComponent component, IllitidThoughtActionEvent args)
    {
        if (args.Handled)
            return;

        var session = TryGetSession(args.Performer);
        if (session == null)
            return;

        args.Handled = true;

        _quickDialog.OpenDialog(session,
            "Отправить мысль",
            "Мысль",
            (string thought) =>
            {
                CheckThought(uid, thought);
                if (SendThought(uid, args.Target, thought))
                    SendEffect(uid, 1000f, 0.05f, ThoughtCast);
                else
                    _popup.PopupEntity("Никто не услышал...", uid, uid);
            });
    }


    private void OnMassThoughtAction(EntityUid uid, IllitidComponent component, IllitidMassThoughtActionEvent args)
    {
        if (args.Handled)
            return;

        var session = TryGetSession(args.Performer);
        if (session == null)
            return;

        args.Handled = true;


        _quickDialog.OpenDialog(session,
            "Отправить мысль",
            "Мысль",
            (string thought) =>
            {
                if (!TryConsumePsi(uid, 3))
                    return;

                CheckThought(uid, thought);
                if(SendThoughtInRange(uid, 7, thought))
                    SendEffect(uid, 1000f, 0.05f, ThoughtCast);
                else
                    _popup.PopupEntity("Никто не услышал...", uid, uid);
            });
    }

    private void OnForceSayAction(EntityUid uid, IllitidComponent component, IllitidForceSayActionEvent args)
    {
        if (args.Handled)
            return;

        var session = TryGetSession(args.Performer);
        if (session == null)
            return;

        args.Handled = true;


        _quickDialog.OpenDialog(session,
            "Сказать",
            "Слова",
            (string thought) =>
            {
                if (!TryConsumePsi(uid, 3))
                    return;

                if (!CheckDistance(uid, args.Target, 15))
                    return;

                if (TryComp<SkillsComponent>(args.Target, out var skills))
                {
                    if (skills.Levels.TryGetValue("Intelligence", out var level) && level > 9)
                    {
                        _popup.PopupEntity("Слишком крепкий разум", uid, uid);
                        return;
                    }
                }

                CheckThought(uid, thought, false);
                _adminLogger.Add(LogType.Chat,
                    LogImpact.Low,
                    $"Say from {ToPrettyString(uid):user} as {ToPrettyString(args.Target):user}: {thought}");

                _chat.TrySendInGameICMessage(args.Target,
                    thought,
                    InGameICChatType.Speak,
                    ChatTransmitRange.Normal,
                    true, checkNrp: false);

                SendEffect(args.Target, 1000f, 0.35f, ForceSayReceive);
                SendEffect(uid, 1000f, 0.05f, ForceSayCast);
            });
    }

    private void OnBlindnessAction(EntityUid uid, IllitidComponent component, IllitidBlindnessActionEvent args)
    {
        if (!TryConsumePsi(uid, 4))
            return;

        _flash.Flash(args.Target, uid, uid, TimeSpan.FromSeconds(1.5f), 0, ignoreFlashAttempt: false, displayPopup: false);
        SendEffect(args.Target, 0f, 0f, BlindnessReceive);
        SendEffect(uid, 1000f, 0.05f, BlindnessCast);
    }
    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IllitidComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.Accumulator += frameTime;

            if (comp.Accumulator <= comp.TimeToNextPsiLevel)
                continue;
            comp.Accumulator -= comp.TimeToNextPsiLevel;

            if (comp.PsiLevel < comp.PsiRegenCap)
            {
                ChangePsiAmount(uid, 1, comp, regenCap: true);
            }
        }
    }

    #region Helpers
    private bool ChangePsiAmount(EntityUid uid, int amount, IllitidComponent? component = null, bool regenCap = false)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (amount < 0 && component.PsiLevel < -amount)
            return false;

        component.PsiLevel += amount;
        Dirty(uid, component);

        if (regenCap)
            component.PsiLevel = Math.Min(component.PsiLevel, component.PsiRegenCap);

        _alerts.ShowAlert(uid, component.PsiAlert);
        return true;
    }

    private bool TryConsumePsi(EntityUid uid, int cost)
    {
        if (ChangePsiAmount(uid, -cost))
            return true;

        _popup.PopupEntity("Недостаточно пси-энергии", uid, uid);
        return false;
    }

    private ICommonSession? TryGetSession(EntityUid entity)
    {
        _playerManager.TryGetSessionByEntity(entity, out var session);
        return session;
    }

    private void CheckThought(EntityUid source, string thought, bool log = true)
    {
        if (log)
        {
            _adminLogger.Add(LogType.Chat,
                LogImpact.Low,
                $"Thought from {ToPrettyString(source):user} as Illitid");
        }

        _nrpMessages.CheckMessage(source, thought);
    }

    private bool SendThoughtInRange(EntityUid source, float range, string thought)
    {

        var coords = _transform.GetMapCoordinates(source);

        var recipients = _lookup.GetEntitiesInRange(coords, range);

        var anyRecipient = false;

        foreach (var recipient in recipients)
        {
            if(recipient == source)
                continue;

            if(SendThought(source, recipient, thought))
                anyRecipient = true;
        }

        return anyRecipient;
    }

    private bool CheckDistance(EntityUid a, EntityUid b, float maxDistance)
    {
        var posA = _transform.GetWorldPosition(a);
        var posB = _transform.GetWorldPosition(b);

        var dist = (posA - posB).Length();

        return !(dist > maxDistance);
    }

    private bool SendThought(EntityUid source, EntityUid target, string thought)
    {
        _playerManager.TryGetSessionByEntity(target, out var session);
        if (session == null)
            return false;


        if (!CheckDistance(source, target, 15))
            return false;

        var msg = $"Голос в вашей голове: {thought}";
        _popup.PopupEntity(msg, target, target, PopupType.Medium);
        _chatManager.ChatMessageToOne(channel: ChatChannel.Server, msg, msg, source, false, session.Channel, Color.Purple);

        SendEffect(target, audio: ThoughtReceive);

        _playerManager.TryGetSessionByEntity(source, out var sourceSession);
        if (sourceSession == null)
            return false;

        var sourceMsg = $"Ваш голос: {thought}";
        _chatManager.ChatMessageToOne(channel: ChatChannel.Server, sourceMsg, sourceMsg, source, false, sourceSession.Channel, Color.Purple);

        return true;
    }


    private void Flash(EntityUid target, float flashDuration = 1000, float strength = 0.15f)
    {
        if (!_statusEffectsSystem.TryAddStatusEffect<IllitidFlashedComponent>(target,
                IllitidFlashedKey,
                TimeSpan.FromSeconds(flashDuration / 1000f),
                true))
            return;


        var flashed = Comp<IllitidFlashedComponent>(target);
        flashed.Strength = strength;
    }

    private void SendEffect(EntityUid target, float flashDuration = 1000, float flashStrength = 0.15f, string? audio = null)
    {
        if(flashDuration > 0 &&  flashStrength > 0)
            Flash(target, flashDuration, flashStrength);

        if(audio != null)
            _audio.PlayGlobal(audio, target);
    }
    #endregion
}
