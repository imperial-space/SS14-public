using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.IdentityManagement;

public sealed class MedievalIdentitySystem : SharedMedievalIdentitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private int _nextId = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, ComponentInit>(OnComponentInit, before: new[] { typeof(SharedMedievalFactionsSystem) });
        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnComponentInit(EntityUid uid, IdentityRequiresKnowledgeComponent component, ComponentInit args)
    {
        component.Identifier = _nextId;
        _nextId++;
        Dirty(uid, component);
    }

    private void OnPlayerAttached(EntityUid uid, IdentityRequiresKnowledgeComponent component, PlayerAttachedEvent args)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return;
        var mindUid = session.GetMind();
        if (!TryComp<MindComponent>(mindUid, out var mind))
            return;
        _adminLogger.Add(LogType.EventRan, LogImpact.Low, $"Player {session.Name} attached to entity with identity id: {component.Identifier}");
    }
}
