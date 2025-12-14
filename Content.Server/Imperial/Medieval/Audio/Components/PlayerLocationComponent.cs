namespace Content.Server.Imperial.Medieval.Audio;

/// <summary>
/// Компонент, хранящий информацию о звуковой зоне игрока.
/// </summary>
[RegisterComponent]
public sealed partial class PlayerLocationComponent : Component
{
    /// <summary>
    /// Текущий идентификатор звуковой локации игрока.
    /// </summary>
    [DataField("currentLocation")]
    public string CurrentLocationId = string.Empty;

    /// <summary>
    /// Предыдущий идентификатор звуковой локации игрока.
    /// </summary>
    [ViewVariables]
    public string PreviousLocationId = string.Empty;

    /// <summary>
    /// Текущий проигрываемый звуковой поток (для игрока).
    /// </summary>
    [ViewVariables]
    public EntityUid? CurrentStream;

    /// <summary>
    /// Предыдущий звуковой поток (который затухает).
    /// </summary>
    [ViewVariables]
    public EntityUid? PreviousStream;

    /// <summary>
    /// Версия таймера случайных звуков (для отмены предыдущих таймеров).
    /// </summary>
    [ViewVariables]
    public int RandomSoundVersion = 0;

    /// <summary>
    /// Целевой уровень громкости в децибелах, когда звук полностью слышен.
    /// </summary>
    [DataField("targetVolumeDb")]
    public float TargetVolumeDb = -6f;

    /// <summary>
    /// Скорость изменения громкости при затухании/нарастании в дБ/сек (по модулю).
    /// </summary>
    [DataField("fadeRateDbPerSec")]
    public float FadeRateDbPerSec = 0.9f; // Вроде норм подобрал дефоул

    /// <summary>
    /// Версия плавного изменения текущего звука (для отмены предыдущих таймеров).
    /// </summary>
    [ViewVariables]
    public int CurrentFadeVersion = 0;

    /// <summary>
    /// Версия плавного затухания предыдущего звука (для отмены предыдущих таймеров).
    /// </summary>
    [ViewVariables]
    public int PreviousFadeVersion = 0;
}
