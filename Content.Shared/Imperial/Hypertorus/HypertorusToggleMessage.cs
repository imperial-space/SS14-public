using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Hypertorus;

[Serializable, NetSerializable]
public sealed class HypertorusToggleMessage(HypertorusToggle toggle) : BoundUserInterfaceMessage
{
    public readonly HypertorusToggle Toggle = toggle;
}
