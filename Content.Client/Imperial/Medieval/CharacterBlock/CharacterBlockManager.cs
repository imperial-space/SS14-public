using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.CharacterBlock;
using Content.Shared.Preferences;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Medieval.CharacterBlock;

public sealed class CharacterBlockManager : IEntityEventSubscriber
{
    [Dependency] private readonly IClientNetManager _netManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private List<string> _blockedCharactersHashes = new();

    public void Initialize()
    {
        IoCManager.InjectDependencies(this);

        _entityManager.EventBus.SubscribeEvent<RoundRestartCleanupEvent>(EventSource.Network, this, OnRoundRestart);

        _netManager.RegisterNetMessage<UpdateBlockerCharactersMessage>(OnCharacterBlockUpdate);
    }

    public bool IsCharacterBlocked(HumanoidCharacterProfile profile)
    {
        var hash = profile.BuildId();

        return _blockedCharactersHashes.Contains(hash);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _blockedCharactersHashes.Clear();
    }

    private void OnCharacterBlockUpdate(UpdateBlockerCharactersMessage message)
    {
        _blockedCharactersHashes = new(message.BlockedCharactersHashes);
    }
}
