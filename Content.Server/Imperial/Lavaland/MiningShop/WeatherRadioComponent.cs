using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.MiningShop;

/// <summary>
/// Шахтёрское погодное радио (radio/weather_monitor + component/weather_announcer):
/// предупреждает о пепельной буре голосом и в канал снабжения.
/// </summary>
[RegisterComponent]
public sealed partial class WeatherRadioComponent : Component
{
    /// <summary>WEATHER_CLEAR_DELAY: дальше — «ясно».</summary>
    [DataField] public float ClearDelay = 120f;

    [DataField] public string Channel = "Supply";

    [ViewVariables] public WeatherAlertLevel Level = WeatherAlertLevel.Clear;
    [ViewVariables] public bool Dangerous = true;
    [ViewVariables] public TimeSpan NextCheck;
}
