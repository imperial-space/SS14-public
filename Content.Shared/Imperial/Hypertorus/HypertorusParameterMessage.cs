using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Hypertorus;

[Serializable, NetSerializable]
public sealed class HypertorusParameterMessage(HypertorusParameter parameter, float value) : BoundUserInterfaceMessage
{
    public readonly HypertorusParameter Parameter = parameter;
    public readonly float Value = value;
}
