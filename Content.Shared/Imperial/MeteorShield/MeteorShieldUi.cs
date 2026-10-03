using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MeteorShield;

[Serializable, NetSerializable]
public enum SatelliteControlUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum SatelliteVisuals : byte
{
    Active,
}

[Serializable, NetSerializable]
public sealed class SatelliteState(int id, bool active, string mode)
{
    public readonly int Id = id;
    public readonly bool Active = active;
    public readonly string Mode = mode;
}

/// <summary>ui_data консоли sat_control (tgui SatelliteControl).</summary>
[Serializable, NetSerializable]
public sealed class SatelliteControlUiState(
    List<SatelliteState> satellites,
    bool meteorShield,
    int coverage,
    int coverageMax) : BoundUserInterfaceState
{
    public readonly List<SatelliteState> Satellites = satellites;
    public readonly bool MeteorShield = meteorShield;
    public readonly int Coverage = coverage;
    public readonly int CoverageMax = coverageMax;
}

[Serializable, NetSerializable]
public sealed class SatelliteToggleMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed partial class SatelliteToggleDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
