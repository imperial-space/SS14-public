using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.MiningShop;

/// <summary>
/// Инжектор Лазаря (item/lazarus_injector из SS13): один раз воскрешает мёртвое животное и делает его другом.
/// После ЭМИ — неисправен: воскрешённый зверь враждебен всем.
/// </summary>
[RegisterComponent]
public sealed partial class LazarusInjectorComponent : Component
{
    [DataField] public bool Loaded = true;
    [DataField] public bool Malfunctioning;
    [DataField] public SoundSpecifier UseSound = new SoundPathSpecifier("/Audio/Effects/refill.ogg");
}

/// <summary>
/// Браслеты Кхейрала (accessory/kheiral_cuffs): надетые вне станции дают GPS-сигнал владельца.
/// </summary>
[RegisterComponent]
public sealed partial class KheiralCuffsComponent : Component
{
    [DataField] public SoundSpecifier EquipSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/cuff_end.ogg");

    [ViewVariables] public EntityUid? Wearer;
    [ViewVariables] public bool GpsEnabled;
    [ViewVariables] public TimeSpan NextCheck;
}

public enum WeatherAlertLevel : byte
{
    Clear,
    Incoming,
    ImminentOrActive,
}

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

/// <summary>Неоткрытая и невскрытая рудная жила — цель вентпойнтера.</summary>
[RegisterComponent]
public sealed partial class LavalandUndiscoveredVentComponent : Component;
