using Content.Server.Administration.Managers;
using Content.Server.Afk;
using Content.Server.Afk.Events;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Imperial.Medieval.JoinQueue;
using Content.Shared.CCVar;
using Content.Shared.Imperial.Medieval.Afk;
using Content.Shared.Imperial.Medieval.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Afk;

public sealed class MedievalAfkSystem : EntitySystem
{
    [Dependency] private readonly IMedievalAfkManager _afkManager = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly JoinQueueManager _joinQueueManager = default!;

    private float _checkDelay;
    private TimeSpan _checkTime;
    private readonly Dictionary<ICommonSession, AfkState> _afkStates = new();

    private readonly Dictionary<ICommonSession, BaseEui> _afkPlayers = new();

    public override void Initialize()
    {
        base.Initialize();
        _playerManager.PlayerStatusChanged += OnPlayerChange;
        Subs.CVar(_configManager, MedievalCCVars.AfkTime, SetAfkDelay, true);

        SubscribeNetworkEvent<MedievalPlayerActionEvent>(HandlePlayerAction);
    }

    private void HandlePlayerAction(MedievalPlayerActionEvent args)
    {
        var session = _playerManager.GetSessionById(args.SessionId);

        _afkManager.PlayerDidAction(session);
    }

    private void SetAfkDelay(float obj)
    {
        _checkDelay = obj;
    }

    private void OnPlayerChange(object? sender, SessionStatusEventArgs e)
    {
        switch (e.NewStatus)
        {
            case SessionStatus.Disconnected:
                _afkPlayers.Remove(e.Session);
                break;
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _afkPlayers.Clear();
        _playerManager.PlayerStatusChanged -= OnPlayerChange;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _checkTime)
            return;

        _checkTime = _timing.CurTime + TimeSpan.FromSeconds(_checkDelay);

        foreach (var pSession in Filter.GetAllPlayers())
        {
            if (pSession.AttachedEntity != null)
                continue;

            if (_joinQueueManager.IsInQueue(pSession))
                continue;

            if (_admin.IsAdmin(pSession, false))
                continue;

            var newState = _afkManager.GetAfkState(pSession);

            if (_afkStates.TryGetValue(pSession, out var oldState) && oldState == newState)
                continue;

            _afkStates[pSession] = newState;

            switch (newState)
            {
                case AfkState.Afk:
                    if (_afkPlayers.ContainsKey(pSession))
                        break;

                    var eui = new MedievalAfkEui();
                    _eui.OpenEui(eui, pSession);
                    _afkPlayers.Add(pSession, eui);
                    break;
                case AfkState.Active:
                    if (!_afkPlayers.TryGetValue(pSession, out var afkEui))
                        break;

                    _eui.CloseEui(afkEui);
                    _afkPlayers.Remove(pSession);
                    break;
                case AfkState.Timeout:
                    _netManager.DisconnectChannel(pSession.Channel, "Afk");
                    break;
            }
        }
    }
}

