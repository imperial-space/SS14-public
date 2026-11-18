using Content.Shared.Imperial.LastWords;
using Robust.Shared.Network;

namespace Content.Client.Imperial.LastWords;

/// <summary>
/// Принимает от сервера последние слова игроков в конце раунда. Сам ничего не запрашивает — сервер шлёт сам.
/// Сообщение может прийти и до, и после открытия окна итогов, поэтому вкладка подписывается на <see cref="Updated"/>.
/// </summary>
public sealed class ImperialLastWordsManager
{
    [Dependency] private readonly IClientNetManager _net = default!;

    public int RoundId { get; private set; } = -1;

    public IReadOnlyList<ImperialLastWordsEntry> Entries { get; private set; } = Array.Empty<ImperialLastWordsEntry>();

    public event Action? Updated;

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgImperialLastWords>(OnMessage);
    }

    private void OnMessage(MsgImperialLastWords message)
    {
        RoundId = message.RoundId;
        Entries = message.Entries;
        Updated?.Invoke();
    }
}
