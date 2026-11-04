using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Hypertorus;

[Serializable, NetSerializable]
public sealed class HypertorusGasEntry(Gas gas, float amount)
{
    public readonly Gas Gas = gas;
    public readonly float Amount = amount;
}
