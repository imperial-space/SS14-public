using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Ghost.Components;
using Content.Server.MagicBarrier.Components;
using Content.Server.Mind;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.CCVar;
using Content.Shared.Imperial.Medieval.MedievalReviveSpawner;
using Content.Shared.Imperial.Medieval.Revive;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Revive;

public sealed class MedievalReviveSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    private const int MaxRevives = 3;

    public override void Initialize()
    {
        SubscribeNetworkEvent<GhostReviveRequestEvent>(OnGhostReviveRequest);
        SubscribeNetworkEvent<ReviveCountRequestEvent>(OnReviveCountRequest);
    }

    private void OnGhostReviveRequest(GhostReviveRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_cfg.GetCVar(MedievalCCVars.GhostRevive))
            return;

        var session = args.SenderSession;
        var userId = session.UserId;

        if (!EntityQuery<MagicBarrierComponent>().TryFirstOrDefault(out var barrier))
            return;

        if (!barrier.ReviveCount.TryGetValue(userId, out var reviveCount))
            reviveCount = 0;

        if (reviveCount >= MaxRevives)
            return;

        if (!_minds.TryGetMind(userId, out _, out var oldMind))
            return;

        var currentEntity = oldMind.CurrentEntity;
        if (currentEntity == null || !HasComp<GhostComponent>(currentEntity))
            return;

        var spawnerQuery = EntityQuery<MedievalReviveSpawnerComponent>();
        if (!spawnerQuery.Any())
            return;

        var spawner = _random.Pick<MedievalReviveSpawnerComponent>(spawnerQuery.ToList());
        var spawnerCoords = _transform.GetMoverCoordinates(spawner.Owner);
        var mob = Spawn(spawner.Prototype, spawnerCoords);

        _transform.AttachToGridOrMap(mob);
        EnsureComp<MindContainerComponent>(mob);

        if (!oldMind.IsVisitingEntity)
            _minds.WipeMind(session);

        var newMind = _minds.CreateMind(userId, Comp<MetaDataComponent>(mob).EntityName);

        _minds.SetUserId(newMind, userId);
        _minds.TransferTo(newMind, mob);

        barrier.ReviveCount[userId] = reviveCount + 1;
    }

    private void OnReviveCountRequest(ReviveCountRequestEvent _, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId = session.UserId;

        if (!EntityQuery<MagicBarrierComponent>().TryFirstOrDefault(out var barrier))
            return;

        if (!barrier.ReviveCount.TryGetValue(userId, out var reviveCount))
            reviveCount = 0;

        RaiseNetworkEvent(new ReviveCountResponseEvent(reviveCount, MaxRevives), session);
    }
}
