using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

[Serializable, NetSerializable]
public sealed class ImperialManufacturingSelectOption
{
    public string Id = string.Empty;
    public string Name = string.Empty;
}
