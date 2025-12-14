using System.Linq;
using Content.Server.Connection;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.ICCVar;
using Content.Shared.Imperial.Medieval.JoinQueue;
using Prometheus;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.JoinQueue;

/// <summary>
/// Manages new player connections when the server is full and queues them up,
/// granting access when a slot becomes free.
/// </summary>
public sealed class JoinQueueManager
{
    private static readonly Gauge QueueCount = Metrics.CreateGauge(
        "join_queue_count",
        "Amount of players in queue."
    );

    private static readonly Counter QueueBypassCount = Metrics.CreateCounter(
        "join_queue_bypass_count",
        "Amount of players who bypassed queue by privileges."
    );

    private static readonly Histogram QueueTimings = Metrics.CreateHistogram(
        "join_queue_timings",
        "Timings of players in queue",
        new HistogramConfiguration
        {
            LabelNames = ["type"],
            Buckets = Histogram.ExponentialBuckets(1, 2, 14),
        }
    );

    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IConnectionManager _connection = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IServerNetManager _net = default!;

    /// <summary>
    /// Queue of active player sessions.
    /// </summary>
    /// <remarks>
    /// Real Queue class can't delete disconnected users.
    /// </remarks>
    private readonly List<ICommonSession> _queue = [];

    public bool IsInQueue(ICommonSession session)
    {
        return _queue.Contains(session);
    }

    private int PlayerInQueueCount => _queue.Count;

    /// <remarks>
    /// Now it's only real value with actual players count that in game.
    /// </remarks>
    private int ActualPlayersCount => _player.PlayerCount - PlayerInQueueCount;

    private bool _enabled;

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgQueueUpdate>();

        _configuration.OnValueChanged(ICCVars.QueueEnabled, OnQueueCVarChanged, true);
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private void OnQueueCVarChanged(bool value)
    {
        _enabled = value;

        if (value)
            return;

        foreach (var session in _queue)
        {
            session.Channel.Disconnect("Queue was disabled");
        }
    }

    private async void OnPlayerConnected(object? sender, SessionStatusEventArgs e)
    {
        if (!_enabled)
        {
            SendToGame(e.Session);
            return;
        }

        var isPrivileged = false;
        if (_connection is ConnectionManager connection)
            isPrivileged = connection.HavePriorityJoin(e.Session.UserId);

        var wasInGame = EntitySystem.TryGet<GameTicker>(out var ticker) && ticker.PlayerGameStatuses.TryGetValue(e.Session.UserId, out var status) &&
                         status == PlayerGameStatus.JoinedGame;

        // Do not count current session in general online, because we are still deciding her fate
        var currentOnline = _player.PlayerCount - 1;
        var haveFreeSlot = currentOnline < _configuration.GetCVar(CCVars.SoftMaxPlayers);

        if (isPrivileged || haveFreeSlot || wasInGame)
        {
            SendToGame(e.Session);

            if (isPrivileged && !haveFreeSlot)
                QueueBypassCount.Inc();

            return;
        }

        _queue.Add(e.Session);
        ProcessQueue(false, e.Session.ConnectedTime);
    }

    private async void OnPlayerDisconnected(object? sender, SessionStatusEventArgs e)
    {
        var wasInQueue = _queue.Remove(e.Session);

        // Process queue only if player disconnected from InGame or from queue
        if (!wasInQueue && e.OldStatus != SessionStatus.InGame)
            return;

        ProcessQueue(true, e.Session.ConnectedTime);

        if (wasInQueue)
            QueueTimings.WithLabels("Unwaited").Observe((DateTime.UtcNow - e.Session.ConnectedTime).TotalSeconds);
    }

    private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        switch (e.NewStatus)
        {
            case SessionStatus.Connected:
                OnPlayerConnected(sender, e);
                break;

            case SessionStatus.Disconnected:
                OnPlayerDisconnected(sender, e);
                break;
        }
    }

    /// <summary>
    /// If possible, takes the first player in the queue and sends him into the game
    /// </summary>
    /// <param name="isDisconnect">Is method called on disconnect event</param>
    /// <param name="connectedTime">Session connected time for histogram metrics</param>
    private void ProcessQueue(bool isDisconnect, DateTime connectedTime)
    {
        var players = ActualPlayersCount;
        if (isDisconnect)
            players--; // Decrease currently disconnected session but that has not yet been deleted

        var haveFreeSlot = players < _configuration.GetCVar(CCVars.SoftMaxPlayers);
        var queueContains = _queue.Count > 0;

        if (haveFreeSlot && queueContains)
        {
            var session = _queue.First();
            _queue.Remove(session);

            SendToGame(session);

            QueueTimings
                .WithLabels("Waited")
                .Observe((DateTime.UtcNow - connectedTime).TotalSeconds);
        }

        SendUpdateMessages();
        QueueCount.Set(_queue.Count);
    }

    /// <summary>
    /// Sends messages to all players in the queue with the current state of the queue
    /// </summary>
    private void SendUpdateMessages()
    {
        for (var i = 0; i < _queue.Count; i++)
        {
            _queue[i]
                .Channel.SendMessage(new MsgQueueUpdate
                {
                    Total = _queue.Count,
                    Position = i + 1,
                }
            );
        }
    }

    /// <summary>
    /// Letting player's session into game, change player state
    /// </summary>
    /// <param name="s">Player session that will be sent to game</param>
    private void SendToGame(ICommonSession s)
    {
        Timer.Spawn(0, () => _player.JoinGame(s));
    }
}
