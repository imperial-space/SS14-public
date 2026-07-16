using Content.Server.Chat.Managers;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Player;
using System.Text;

namespace Content.Server.Imperial.Lavaland.MegafaunaTracker;

public sealed class MegafaunaTrackerSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MegafaunaTrackerComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(EntityUid uid, MegafaunaTrackerComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var userXform = Transform(args.User);

        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("megafauna-tracker-header"));

        var found = false;
        var query = EntityQueryEnumerator<LavalandMegafaunaComponent, TransformComponent, MobStateComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out _, out var xform, out var mobState, out var meta))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            if (xform.MapUid != userXform.MapUid)
                continue;

            var pos = xform.WorldPosition;
            sb.AppendLine(Loc.GetString("megafauna-tracker-entry",
                ("name", meta.EntityName),
                ("x", (int)pos.X),
                ("y", (int)pos.Y)));
            found = true;
        }

        if (!found)
            sb.AppendLine(Loc.GetString("megafauna-tracker-none"));

        _chatManager.DispatchServerMessage(actor.PlayerSession, sb.ToString().TrimEnd());
        args.Handled = true;
    }
}
