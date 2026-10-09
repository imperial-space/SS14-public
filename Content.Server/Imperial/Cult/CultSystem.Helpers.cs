using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Cult;

public sealed partial class CultSystem
{
    private readonly Dictionary<EntityUid, Action<string>> _pendingChoice = new();
    private readonly Dictionary<EntityUid, Action<string>> _pendingText = new();

    private void InitializeMenus()
    {
        SubscribeLocalEvent<CultChoiceMessage>(OnChoiceMessage);
        SubscribeLocalEvent<CultTextMessage>(OnTextMessage);
    }

    /// <summary>tgui_input_list / show_radial_menu: выбор из вариантов.</summary>
    public void OpenChoice(EntityUid user, string title, List<CultMenuOption> options, Action<string> callback, bool radial = true)
    {
        if (!TryComp<ActorComponent>(user, out _))
            return;

        _ui.SetUi(user, CultMenuUiKey.Choice, new InterfaceData("CultChoiceBoundUserInterface", -1f, false));
        _pendingChoice[user] = callback;
        _ui.CloseUi(user, CultMenuUiKey.Choice);
        _ui.SetUiState(user, CultMenuUiKey.Choice, new CultChoiceState(title, options, radial));
        _ui.OpenUi(user, CultMenuUiKey.Choice, user);
    }

    /// <summary>tgui_alert: да/нет.</summary>
    public void OpenConfirm(EntityUid user, string title, string yes, string no, Action callback)
    {
        OpenChoice(user, title, new List<CultMenuOption>
        {
            new("yes", yes),
            new("no", no),
        }, id =>
        {
            if (id == "yes")
                callback();
        }, radial: false);
    }

    /// <summary>tgui_input_text.</summary>
    public void OpenText(EntityUid user, string title, string prompt, int maxLength, Action<string> callback)
    {
        if (!TryComp<ActorComponent>(user, out _))
            return;

        _ui.SetUi(user, CultMenuUiKey.Text, new InterfaceData("CultTextBoundUserInterface", -1f, false));
        _pendingText[user] = callback;
        _ui.CloseUi(user, CultMenuUiKey.Text);
        _ui.SetUiState(user, CultMenuUiKey.Text, new CultTextState(title, prompt, maxLength));
        _ui.OpenUi(user, CultMenuUiKey.Text, user);
    }

    private void OnChoiceMessage(CultChoiceMessage msg)
    {
        var user = GetEntity(msg.Entity);
        _ui.CloseUi(user, CultMenuUiKey.Choice);
        if (msg.Actor != user || !_pendingChoice.Remove(user, out var callback))
            return;
        callback(msg.Id);
    }

    private void OnTextMessage(CultTextMessage msg)
    {
        var user = GetEntity(msg.Entity);
        _ui.CloseUi(user, CultMenuUiKey.Text);
        if (msg.Actor != user || !_pendingText.Remove(user, out var callback))
            return;
        var text = msg.Text.Trim();
        if (text.Length == 0)
            return;
        callback(text);
    }

    // ───── Речь ─────

    /// <summary>say(invocation, forced = "cult invocation").</summary>
    public void Say(EntityUid uid, string text)
    {
        _chat.TrySendInGameICMessage(uid, text, InGameICChatType.Speak, ChatTransmitRange.Normal, ignoreActionBlocker: true);
    }

    /// <summary>whisper(invocation).</summary>
    public void Whisper(EntityUid uid, string text)
    {
        _chat.TrySendInGameICMessage(uid, text, InGameICChatType.Whisper, ChatTransmitRange.Normal, ignoreActionBlocker: true);
    }

    /// <summary>to_chat одному игроку.</summary>
    public void Message(EntityUid uid, string text)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;
        _chatManager.ChatMessageToOne(ChatChannel.Server, text, text, EntityUid.Invalid, false, actor.PlayerSession.Channel);
    }

    /// <summary>Сообщение всем культистам (и призракам, если ghosts).</summary>
    public void MessageCult(string text, bool ghosts = false)
    {
        var filter = Filter.Empty().AddWhereAttachedEntity(e => IsCultAligned(e) || ghosts && HasComp<GhostComponent>(e));
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, text, text, EntityUid.Invalid, false, true, Color.FromHex("#960000"));
    }

    /// <summary>SEND_SOUND каждому культисту.</summary>
    public void SoundCult(SoundSpecifier sound, float volume = 0f)
    {
        var filter = Filter.Empty().AddWhereAttachedEntity(IsCultAligned);
        _audio.PlayGlobal(sound, filter, true, AudioParams.Default.WithVolume(volume));
    }

    public void SoundTo(EntityUid uid, SoundSpecifier sound, float volume = 0f)
    {
        _audio.PlayGlobal(sound, Filter.Entities(uid), true, AudioParams.Default.WithVolume(volume));
    }

    /// <summary>Громкость BYOND (0–100) в децибелы.</summary>
    public static float Db(float volume) => volume >= 100 ? 0f : MathF.Max(-20f, 20f * MathF.Log10(Math.Max(volume, 1) / 100f));

    public void Damage(EntityUid uid, string type, float amount, bool ignoreResistances = true, EntityUid? origin = null)
    {
        if (amount <= 0)
            return;
        var spec = new DamageSpecifier();
        spec.DamageDict[type] = amount;
        _damageable.TryChangeDamage(uid, spec, ignoreResistances, origin: origin);
    }

    /// <summary>Временный визуальный эффект.</summary>
    public EntityUid Effect(string proto, Robust.Shared.Map.EntityCoordinates coords, Angle? rotation = null, Color? color = null, bool randomDir = false)
    {
        var uid = Spawn(proto, coords);
        if (rotation != null)
            _transform.SetLocalRotation(uid, rotation.Value);
        else if (randomDir)
            _transform.SetLocalRotation(uid, Angle.FromDegrees(90 * _random.Next(4)));
        if (color != null)
            _appearance.SetData(uid, CultVisuals.Color, color.Value);
        return uid;
    }
}
