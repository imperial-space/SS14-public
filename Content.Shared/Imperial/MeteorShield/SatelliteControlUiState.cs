using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MeteorShield;

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
