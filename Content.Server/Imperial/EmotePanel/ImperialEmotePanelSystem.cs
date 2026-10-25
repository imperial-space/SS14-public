using Content.Server.Chat.Systems;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Imperial.EmotePanel;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.EmotePanel;

/// <summary>Действие панели эмоций и состояние эмоций существа.</summary>
[RegisterComponent]
public sealed partial class ImperialEmotePanelComponent : Component
{
    public EntityUid? Action;
    public TimeSpan NextCustomEmote;

    /// <summary>emotes_used: когда каждая эмоция снова доступна.</summary>
    public Dictionary<string, TimeSpan> Cooldowns = new();

    /// <summary>MANUAL_GENERAL_EMOTE_AUDIO_COOLDOWN.</summary>
    public TimeSpan NextSound;

    /// <summary>MANUAL_SPECIFIC_EMOTE_AUDIO_COOLDOWN по эмоциям.</summary>
    public Dictionary<string, TimeSpan> NextSpecificSound = new();

    /// <summary>COOLDOWN_YAWN_PROPAGATION.</summary>
    public TimeSpan YawnImmuneUntil;

    /// <summary>client.recent_examines: кого и когда осматривало существо.</summary>
    public Dictionary<EntityUid, TimeSpan> RecentExamines = new();
}

/// <summary>Выдаёт действие панели эмоций и исполняет свои эмоции игрока.</summary>
public sealed class ImperialEmotePanelSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ImperialEmoteSystem _emote = default!;

    private static readonly EntProtoId PanelAction = "ImperialActionEmotePanel";

    /// <summary>CUSTOM_EMOTE_COOLDOWN.</summary>
    private static readonly TimeSpan CustomCooldown = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<ImperialCustomEmoteMessage>(OnCustomEmote);
    }

    /// <summary>Панель есть у всех живых существ и у ИИ, но не у призраков.</summary>
    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Entity) && !HasComp<StationAiHeldComponent>(args.Entity))
            return;

        var panel = EnsureComp<ImperialEmotePanelComponent>(args.Entity);
        if (panel.Action == null)
            _actions.AddAction(args.Entity, ref panel.Action, PanelAction);
    }

    private void OnCustomEmote(ImperialCustomEmoteMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid || !TryComp<ImperialEmotePanelComponent>(uid, out var panel))
            return;

        var text = msg.Text.Trim();
        if (text.Length == 0 || text.Length > ImperialEmotePanel.MaxTextLength)
            return;

        var now = _timing.CurTime;
        if (now < panel.NextCustomEmote)
        {
            _popup.PopupEntity(Loc.GetString("imperial-emote-panel-too-fast"), uid, uid);
            return;
        }

        panel.NextCustomEmote = now + CustomCooldown;

        // TGUI_PANEL_EMOTE_TYPE_CUSTOM: свой текст, но звук и действие эмоции остаются.
        if (msg.Emote is { } id && _proto.TryIndex<ImperialEmotePrototype>(id, out var emote))
        {
            _emote.TryEmote(uid, emote, null, intentional: true, messageOverride: text);
            return;
        }

        _chat.TrySendInGameICMessage(uid, text, InGameICChatType.Emote, false);
    }
}
