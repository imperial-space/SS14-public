using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishingPortalOption
{
    public string Key = string.Empty;
    public string Name = string.Empty;
    public string RadialState = "default";

    /// <summary>Привязанное место ловли (звёздочка в углу).</summary>
    public bool Linked;
}
