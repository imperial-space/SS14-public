using Content.Server.Chat.Systems;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.EmotePanel;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.EmotePanel;

/// <summary>Действие панели эмоций у существа.</summary>
[RegisterComponent]
public sealed partial class ImperialEmotePanelComponent : Component
{
    public EntityUid? Action;
    public TimeSpan NextCustomEmote;
}

/// <summary>Выдаёт действие панели эмоций и исполняет свои эмоции игрока.</summary>
public sealed class ImperialEmotePanelSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly EntProtoId PanelAction = "ImperialActionEmotePanel";

    /// <summary>CUSTOM_EMOTE_COOLDOWN.</summary>
    private static readonly TimeSpan CustomCooldown = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<ImperialCustomEmoteMessage>(OnCustomEmote);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(args.Entity))
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

        EmotePrototype? emote = null;
        if (msg.Emote is { } id && (!_proto.TryIndex(id, out emote) || !_chat.AllowedToUseEmote(uid, emote)))
            return;

        _chat.TrySendInGameICMessage(uid, text, InGameICChatType.Emote, false);

        // Свой текст, но звук и анимация стандартной эмоции остаются.
        if (emote != null)
            _chat.TryEmoteWithoutChat(uid, emote);
    }
}
