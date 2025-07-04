using Content.Shared.Friends.Components;
using Content.Shared.Actions;
using Robust.Shared.Player;
using Content.Server.Administration;
using Content.Shared.Speech;
using Content.Server.Chat.Systems;
using Content.Shared.Friends;

namespace Content.Server.Friends;

public sealed partial class FriendsSystem : SharedFriendsSystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;


    public override void Initialize()
    {
        base.Initialize();
        InitializeRelations();
        InitializeWanted();
        InitializeMenu();

        SubscribeLocalEvent<CloackMessageComponent, ComponentStartup>(OnStart);
        SubscribeLocalEvent<CloackMessageComponent, CloackMessageActionEvent>(OnCloackMessageAction);
    }

    public void OnCloackMessageAction(EntityUid uid, CloackMessageComponent comp, CloackMessageActionEvent args)
    {
        if (!_sharedPlayerManager.TryGetSessionByEntity(uid, out var session)) return;
        _quickDialog.OpenDialog(session, "Весть", "Сообщение", (string message) =>
        {
            var query = EntityQueryEnumerator<CloackRecieverComponent>();
            while (query.MoveNext(out var cloackOwner, out var cloack))
            {
                EnsureComp<SpeechComponent>(cloackOwner);
                if (cloack.Faction == comp.Faction)
                    _chat.TrySendInGameICMessage(cloackOwner, message, InGameICChatType.Whisper, false);
            }

            _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Whisper, false);
            args.Handled = true;
        });
    }

    public void OnStart(EntityUid uid, CloackMessageComponent comp, ComponentStartup args)
    {
        _action.AddAction(uid, comp.Action, uid);
    }
}
