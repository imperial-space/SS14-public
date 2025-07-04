using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.CharacterBlock;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.CharacterBlocker;

public sealed class CharacterBlockSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private Dictionary<NetUserId, List<string>> _blockedCharacters = new();

    public override void Initialize()
    {
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

        _netManager.RegisterNetMessage<UpdateBlockerCharactersMessage>();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs ev)
    {
        if (ev.NewStatus != SessionStatus.Connected)
            return;

        if (!_blockedCharacters.TryGetValue(ev.Session.UserId, out var blockedCharacters))
        {
            return;
        }

        var message = new UpdateBlockerCharactersMessage
        {
            BlockedCharactersHashes = blockedCharacters
        };

        _netManager.ServerSendMessage(message, ev.Session.Channel);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        var userId = ev.Player.UserId;
        var id = ev.Profile.BuildId();

        if (_blockedCharacters.TryGetValue(ev.Player.State.UserId, out var blockedCharacters))
        {
            if (!blockedCharacters.Contains(id))
            {
                blockedCharacters.Add(id);
            }
        }
        else
        {
            _blockedCharacters.Add(ev.Player.State.UserId, [id]);
        }

        var message = new UpdateBlockerCharactersMessage()
        {
            BlockedCharactersHashes = _blockedCharacters[userId]
        };

        _netManager.ServerSendMessage(message, ev.Player.Channel);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        ClearBlockedCharacters();
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        ClearBlockedCharacters();
    }

    private void ClearBlockedCharacters()
    {
        _blockedCharacters.Clear();
    }
}
