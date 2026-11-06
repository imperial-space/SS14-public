using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BluespaceTap;

/// <summary>ui_data из bluespace_tap.dm.</summary>
[Serializable, NetSerializable]
public sealed class BluespaceTapUiState : BoundUserInterfaceState
{
    public float DesiredMiningPower;
    public float MiningPower;
    public float Points;
    public float TotalPoints;
    public float PowerUse;
    public float AvailablePower;
    public bool Emagged;
    public bool Dirty;
    public bool AutoShutdown;
    public bool Stabilizers;
    public float StabilizerPower;
    public bool StabilizerPriority;
    public bool Portaling;
    public List<BluespaceTapProductState> Products = new();
}
