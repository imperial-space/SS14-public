namespace Content.Server.Imperial.Lavaland.Gps;

/// <summary>GPS-устройство (gps/item): открывает окно со списком сигналов.</summary>
[RegisterComponent]
public sealed partial class ImperialGpsDeviceComponent : Component
{
    /// <summary>Автообновление списка.</summary>
    [ViewVariables] public bool Updating = true;

    /// <summary>global_mode: показывать сигналы со всех карт.</summary>
    [ViewVariables] public bool GlobalMode = true;

    [DataField] public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1f);
    [DataField] public TimeSpan EmpDuration = TimeSpan.FromSeconds(30f);

    [ViewVariables] public TimeSpan NextUpdate;
    [ViewVariables] public TimeSpan EmpEnd;
    [ViewVariables] public string BaseName = string.Empty;
}
