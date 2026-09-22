using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic;

[Serializable, NetSerializable]
public enum HereticMawedCrucibleUiKey : byte { Key }

[Serializable, NetSerializable]
public enum HereticCruciblePotionType : byte
{
    Soul = 0,
    Clarity = 1,
    Marshal = 2,
}

[Serializable, NetSerializable]
public sealed class HereticCrucibleSelectMessage : BoundUserInterfaceMessage
{
    public HereticCruciblePotionType Potion { get; }
    public HereticCrucibleSelectMessage(HereticCruciblePotionType potion) => Potion = potion;
}

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
