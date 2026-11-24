using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.LastWords;

/// <summary>Последние слова одного игрока для итогов раунда.</summary>
[Serializable, NetSerializable]
public sealed class ImperialLastWordsEntry
{
    /// <summary>Тело игрока — для спрайта (нет, если тело удалено).</summary>
    public NetEntity? Entity;

    public string Name = string.Empty;

    /// <summary>Должность — заголовок карточки.</summary>
    public string Job = string.Empty;

    public string Phrase = string.Empty;

    /// <summary>Умер к концу раунда (иначе — был при смерти, но выжил).</summary>
    public bool Dead;
}
