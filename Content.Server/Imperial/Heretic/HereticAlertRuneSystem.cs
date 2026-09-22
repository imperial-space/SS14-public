using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Pinpointer;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticAlertRuneSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticAlertRuneComponent, StepTriggerAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<HereticAlertRuneComponent, StepTriggeredOnEvent>(OnTriggered);
    }

    private void OnAttempt(Entity<HereticAlertRuneComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = args.Tripper != ent.Comp.HereticUid;
    }

    private void OnTriggered(Entity<HereticAlertRuneComponent> ent, ref StepTriggeredOnEvent args)
    {
        var hereticUid = ent.Comp.HereticUid;
        if (!Exists(hereticUid))
            return;

        if (!TryComp<ActorComponent>(hereticUid, out var actor))
            return;

        var targetName = Name(args.Tripper);
        var beaconName = FindNearestBeacon(Transform(ent.Owner).MapPosition);

        var rawMsg = Loc.GetString("heretic-carving-alert-rune-triggered",
            ("name", targetName),
            ("beacon", beaconName));
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", rawMsg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, rawMsg, wrapped, default, false, actor.PlayerSession.Channel);
    }

    private string FindNearestBeacon(MapCoordinates runePos)
    {
        var nearest = string.Empty;
        var minDist = float.MaxValue;

        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out _, out var beacon, out var xform))
        {
            if (!beacon.Enabled)
                continue;

            var beaconPos = xform.MapPosition;
            if (beaconPos.MapId != runePos.MapId)
                continue;

            var dist = (beaconPos.Position - runePos.Position).Length();
            if (dist < minDist)
            {
                minDist = dist;
                nearest = beacon.Text ?? string.Empty;
            }
        }

        return string.IsNullOrEmpty(nearest) ? "???" : nearest;
    }
}
