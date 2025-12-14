using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Actions;
using Robust.Shared.Player;
using Content.Server.Administration;
using Content.Shared.Speech;
using Content.Server.Chat.Systems;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Server.Imperial.Medieval.Factions.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using Robust.Shared.Timing;
using Content.Shared.Buckle.Components;
using Content.Shared.Mobs;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class MedievalFactionsSystem : SharedMedievalFactionsSystem
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

        SubscribeLocalEvent<GallowsComponent, StrappedEvent>(OnGallowsStrapped);
        SubscribeLocalEvent<GallowsComponent, UnstrappedEvent>(OnGallowsUnstrapped);
        SubscribeLocalEvent<HangedComponent, MobStateChangedEvent>(OnHangedMobStateChanged);
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

    private void OnGallowsStrapped(EntityUid uid, GallowsComponent comp, ref StrappedEvent args)
    {
        var hanged = EnsureComp<HangedComponent>(args.Buckle);
        hanged.Gallows = uid;
    }

    private void OnGallowsUnstrapped(EntityUid uid, GallowsComponent comp, ref UnstrappedEvent args)
        => RemComp<HangedComponent>(args.Buckle);

    private void OnHangedMobStateChanged(EntityUid uid, HangedComponent comp, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (TryComp<MedievalFactionMemberComponent>(uid, out var member) && TryComp<GallowsComponent>(comp.Gallows, out var gallows))
        {
            if (gallows.OwningFaction == null)
                return;

            if (!TryGetFactionDataContainer(out var factionData))
                return;

            factionData.Value.Comp.Executions.GetOrNew(gallows.OwningFaction.Value);
            var dict = factionData.Value.Comp.Executions[gallows.OwningFaction.Value];
            if (!dict.TryGetValue(member.Faction, out var count))
                dict[member.Faction] = 0;

            dict[member.Faction]++;
        }
    }
}
