using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Flesh;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic.Paths.Flesh;

/// <summary>
/// Способности пути Плоти.
/// </summary>
public sealed class HereticFleshActionsSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private static readonly EntProtoId ShedHumanFormProto = "ActionHereticShedHumanForm";

    private static readonly EntProtoId HereticLivingHeart = "HereticLivingHeart";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticVoicelessDeadComponent, MobStateChangedEvent>(OnVoicelessDeadDied);
        SubscribeLocalEvent<HereticVoicelessDeadComponent, PlayerAttachedEvent>(OnVoicelessDeadPlayerAttached);
        SubscribeLocalEvent<HereticPathAscendedEvent>(OnPathAscended);
    }

    // ─── Flesh familiars ─────────────────────────────────────────────────────

    private void OnVoicelessDeadDied(EntityUid uid, HereticVoicelessDeadComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        Spawn(HereticLivingHeart, Transform(uid).Coordinates);
    }

    private void OnVoicelessDeadPlayerAttached(EntityUid uid, HereticVoicelessDeadComponent comp, PlayerAttachedEvent args)
    {
        var rawMsg = Loc.GetString("heretic-voiceless-dead-chat");
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", rawMsg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, rawMsg, wrapped, default, false, args.Player.Channel);
    }

    private void OnPathAscended(ref HereticPathAscendedEvent args)
    {
        if (args.Component.CurrentPath != HereticPath.Flesh)
            return;

        var uid = args.Heretic;

        _actions.AddAction(uid, ShedHumanFormProto);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-flesh"), uid, uid, PopupType.Large);
    }
}
