namespace Content.Server.Imperial.LastWords.Components;

/// <summary>
/// Тело игрока, чьи последние слова учитываются (вешается при вселении игрока в моба). Хранит последнюю фразу;
/// если она сказана не раньше чем за <see cref="Window"/> до критического состояния — это последние слова.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ImperialLastWordsComponent : Component
{
    /// <summary>Сколько времени до критического состояния фраза ещё считается последней.</summary>
    [DataField]
    public TimeSpan Window = TimeSpan.FromSeconds(5);

    /// <summary>Последняя сказанная фраза.</summary>
    [ViewVariables]
    public string? Recent;

    /// <summary>Когда сказана <see cref="Recent"/>.</summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan RecentAt;

    /// <summary>Запись последних слов этого тела (<see cref="ImperialLastWordsRecordComponent"/>), если есть.</summary>
    [ViewVariables]
    public EntityUid? Record;
}
