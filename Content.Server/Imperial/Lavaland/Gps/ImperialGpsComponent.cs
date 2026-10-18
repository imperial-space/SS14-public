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

/// <summary>GPS-устройство (gps/item): открывает окно со списком сигналов.</summary>
[RegisterComponent]
public sealed partial class ImperialGpsDeviceComponent : Component
{
    /// <summary>Автообновление списка.</summary>
    [ViewVariables] public bool Updating = true;

    /// <summary>global_mode: показывать сигналы со всех карт.</summary>
    [ViewVariables] public bool GlobalMode = true;

    [DataField] public float UpdateInterval = 1f;
    [DataField] public float EmpDuration = 30f;

    [ViewVariables] public TimeSpan NextUpdate;
    [ViewVariables] public TimeSpan EmpEnd;
    [ViewVariables] public string BaseName = string.Empty;
}
