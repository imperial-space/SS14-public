using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

[Serializable, NetSerializable]
public sealed class FissionMonitorUiState : BoundUserInterfaceState
{
    public bool HasReactor;
    public bool Controlling;
    public bool Venting;
    public float Integrity;
    public float PowerKilowatts;
    public float Temperature;
    public float Pressure;
    public float Coefficient;
    public float Throttle;
    public float OperatingPower;
    public List<FissionGasEntry> Gases = new();
    public List<FissionGasEntry> ModeratorGases = new();
}
