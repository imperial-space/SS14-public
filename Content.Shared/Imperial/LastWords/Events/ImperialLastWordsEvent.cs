using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.LastWords.Events;

/// <summary>Сервер → все клиенты, в конце раунда: последние слова игроков перед смертью.</summary>
[Serializable, NetSerializable]
public sealed class ImperialLastWordsEvent(int roundId, List<ImperialLastWordsEntry> entries) : EntityEventArgs
{
    public readonly int RoundId = roundId;

    public readonly List<ImperialLastWordsEntry> Entries = entries;
}
