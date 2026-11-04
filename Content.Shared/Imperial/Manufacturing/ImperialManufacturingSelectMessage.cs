using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

[Serializable, NetSerializable]
public sealed class ImperialManufacturingSelectMessage(string purpose, string id) : BoundUserInterfaceMessage
{
    public readonly string Purpose = purpose;
    public readonly string Id = id;
}
