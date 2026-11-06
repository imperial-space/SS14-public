using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic.Items;

[Serializable, NetSerializable]
public enum HereticCruciblePotionType : byte
{
    Soul = 0,
    Clarity = 1,
    Marshal = 2,
}
