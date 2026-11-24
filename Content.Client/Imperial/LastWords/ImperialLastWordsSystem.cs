using Content.Shared.Imperial.LastWords;
using Content.Shared.Imperial.LastWords.Events;

namespace Content.Client.Imperial.LastWords;

/// <summary>
/// Принимает от сервера последние слова игроков в конце раунда. Событие может прийти и до, и после открытия окна
/// итогов, поэтому вкладка подписывается на <see cref="Updated"/>.
/// </summary>
public sealed class ImperialLastWordsSystem : EntitySystem
{
    public int RoundId { get; private set; } = -1;

    public IReadOnlyList<ImperialLastWordsEntry> Entries { get; private set; } = Array.Empty<ImperialLastWordsEntry>();

    public event Action? Updated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialLastWordsEvent>(OnLastWords);
    }

    private void OnLastWords(ImperialLastWordsEvent ev)
    {
        RoundId = ev.RoundId;
        Entries = ev.Entries;
        Updated?.Invoke();
    }
}
