using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.Gps;

[Serializable, NetSerializable]
public enum ImperialGpsUiKey : byte
{
    Key,
}

/// <summary>Сигнал GPS в списке (entrytag, coords, dist, degrees).</summary>
[Serializable, NetSerializable]
public sealed class ImperialGpsSignal
{
    public string Tag = string.Empty;
    public string Coords = string.Empty;
    public int? Distance;
    public int? Degrees;
}

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

[Serializable, NetSerializable]
public sealed class ImperialGpsPowerMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ImperialGpsUpdatingMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ImperialGpsGlobalModeMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ImperialGpsRefreshMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ImperialGpsRenameMessage(string tag) : BoundUserInterfaceMessage
{
    public string Tag = tag;
}
