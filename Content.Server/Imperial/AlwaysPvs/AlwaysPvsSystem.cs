using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.PVS;
public sealed class AlwaysPvsSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _ent = default!;
    private Dictionary<NetUserId, List<EntityUid>> _override = new();
    public void Initialize()
    {
        IoCManager.InjectDependencies(this);
        _playerManager.PlayerStatusChanged += StatusChanged;
        _ent.EntityDeleted += OnDeleted;
    }
    public void AddForceSend(EntityUid entity, ICommonSession session)
    {
        _override.GetOrNew(session.Channel.UserId).Add(entity);
        _ent.System<PvsOverrideSystem>().AddForceSend(entity, session);
    }
    public void RemoveForceSend(EntityUid entity, ICommonSession session)
    {
        try
        {
            _override.GetOrNew(session.Channel.UserId).Remove(entity);
            _ent.System<PvsOverrideSystem>().RemoveForceSend(entity, session);
        }
        catch { }
    }
    private void OnDeleted(Entity<MetaDataComponent> entity)
    {
        foreach (var entry in _override)
        {
            if (entry.Value.Contains(entity.Owner))
                entry.Value.Remove(entity.Owner);
        }
    }
    private void StatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (_override.ContainsKey(args.Session.Channel.UserId) && args.NewStatus == Robust.Shared.Enums.SessionStatus.Connected)
        {
            var overridet = _override[args.Session.Channel.UserId];
            var delete = new List<EntityUid>();
            foreach (var entity in overridet)
            {
                if (_ent.Deleted(entity))
                {
                    delete.Add(entity);
                    continue;
                }
                _ent.System<PvsOverrideSystem>().AddForceSend(entity, args.Session);
            }
            foreach (var entity in delete)
            {
                overridet.Remove(entity);
            }
            _override[args.Session.Channel.UserId] = overridet;
        }
    }

}

