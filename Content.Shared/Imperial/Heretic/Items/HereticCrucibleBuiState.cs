using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic.Items;

[Serializable, NetSerializable]
public sealed class HereticCrucibleBuiState : BoundUserInterfaceState
{
    public int Charges;
    public int MaxCharges;

    public HereticCrucibleBuiState(int charges, int maxCharges)
    {
        Charges = charges;
        MaxCharges = maxCharges;
    }
}
