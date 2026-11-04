using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Hypertorus;

/// <summary>«filter»: переключить газ в фильтре модератора.</summary>
[Serializable, NetSerializable]
public sealed class HypertorusFilterMessage(Gas gas) : BoundUserInterfaceMessage
{
    public readonly Gas Gas = gas;
}
