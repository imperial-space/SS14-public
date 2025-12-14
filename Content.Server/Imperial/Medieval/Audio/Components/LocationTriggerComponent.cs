using Robust.Shared.Audio;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;

namespace Content.Server.Imperial.Medieval.Audio;

/// <summary>
/// Компонент-триггер, который активируется при приближении
/// игрока(с PlayerLocationComponent) и запускает звук.
/// </summary>
[RegisterComponent]
public sealed partial class LocationTriggerComponent : Component
{
    /// <summary>
    /// Уникальный идентификатор локации.
    /// </summary>
    [DataField("locationId")]
    public string LocationId = string.Empty;

    /// <summary>
    /// Звук, который будет проигрываться при активации триггера.
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Список звуков для случайного воспроизведения во время нахождения в зоне.
    /// </summary>
    [DataField("randomSounds")]
    public List<SoundSpecifier> RandomSounds = new();

    /// <summary>
    /// Минимальное время в секундах между случайными звуками.
    /// </summary>
    [DataField("minRandomInterval")]
    public int MinRandomIntervalSeconds = 10;

    /// <summary>
    /// Максимальное время в секундах между случайными звуками.
    /// </summary>
    [DataField("maxRandomInterval")]
    public int MaxRandomIntervalSeconds = 20;

    /// <summary>
    /// ID фикстуры для проверки коллизий.
    /// </summary>
    [DataField("fixtureId")]
    public string FixtureId = "trigger";

    // TODO: Система день/ночь для эмбиента
    // [DataField("daySound")]
    // public SoundSpecifier? DaySound;
    // [DataField("nightStartHour")]
    // public int DayStartHour = 6;
    // [DataField("nightStartHour")]
    // public int NightStartHour = 18;
    // [DataField("enableTimeBasedAudio")]
    // public bool EnableTimeBasedAudio = false;
}
