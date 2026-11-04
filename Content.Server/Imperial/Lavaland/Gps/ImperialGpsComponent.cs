namespace Content.Server.Imperial.Lavaland.Gps;

/// <summary>
/// Источник GPS-сигнала (datum/component/gps из SS13): виден всем GPS-устройствам.
/// Есть у самих устройств, у мегафауны и у найденных гейзеров и жил.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialGpsComponent : Component
{
    /// <summary>gpstag.</summary>
    [DataField]
    public string Tag = "COM0";

    /// <summary>tracking: выключенный сигнал не виден.</summary>
    [DataField]
    public bool Tracking = true;

    /// <summary>Локализуемый тег (сигналы мегафауны), имеет приоритет над Tag.</summary>
    [DataField]
    public LocId? TagLoc;

    [ViewVariables]
    public bool Emped;
}
