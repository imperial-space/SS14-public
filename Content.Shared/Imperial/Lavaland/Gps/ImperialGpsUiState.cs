using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.Gps;

/// <summary>ui_data компонента gps/item.</summary>
[Serializable, NetSerializable]
public sealed class ImperialGpsUiState : BoundUserInterfaceState
{
    public bool Power;
    public string Tag = string.Empty;
    public bool Updating;
    public bool GlobalMode;
    public bool Emped;
    public string CurrentArea = string.Empty;
    public string CurrentCoords = string.Empty;
    public List<ImperialGpsSignal> Signals = new();
}
