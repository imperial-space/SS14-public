using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Storm;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class LavalandMapComponent : Component
{
    [DataField]
    public TimeSpan MinStormInterval = TimeSpan.FromMinutes(8);

    [DataField]
    public TimeSpan MaxStormInterval = TimeSpan.FromMinutes(12);

    [DataField]
    public TimeSpan WarningDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan StormDuration = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan StormEndingDuration = TimeSpan.FromSeconds(5);

    /// <summary>Погода предупреждения и стихания бури.</summary>
    [DataField]
    public EntProtoId LightWeather = "WeatherAshfallLight";

    /// <summary>Погода самой бури.</summary>
    [DataField]
    public EntProtoId HeavyWeather = "WeatherAshfallHeavy";

    [DataField]
    public float NoEventChance = 0.1f;

    [DataField]
    public float PassByChance = 0.1f;

    /// <summary>Когда начнётся следующая буря (в покое).</summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextStorm;

    [ViewVariables]
    public LavalandStormState StormState = LavalandStormState.Idle;

    /// <summary>Конец текущей фазы бури.</summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan StateEnd;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextDamage;
}

public enum LavalandStormState : byte
{
    Idle,
    Warning,
    PassingBy,
    Active,
    Ending,
}
