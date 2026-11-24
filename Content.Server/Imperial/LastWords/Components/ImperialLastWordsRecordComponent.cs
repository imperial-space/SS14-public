namespace Content.Server.Imperial.LastWords.Components;

/// <summary>
/// Зафиксированные последние слова — отдельная сущность в нуль-пространстве, чтобы запись (с именем и должностью на
/// момент критического состояния) пережила удаление тела. Удаляется вместе со всеми сущностями при рестарте раунда.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialLastWordsRecordComponent : Component
{
    [ViewVariables]
    public string Phrase = string.Empty;

    [ViewVariables]
    public string Name = string.Empty;

    [ViewVariables]
    public string Job = string.Empty;

    /// <summary>Тело; к концу раунда может быть удалено.</summary>
    [ViewVariables]
    public EntityUid? Body;
}
