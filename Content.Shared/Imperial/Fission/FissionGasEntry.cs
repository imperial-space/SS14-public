using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

[Serializable, NetSerializable]
public sealed class FissionGasEntry
{
    public string Gas;
    public float Amount;
    public float Portion;
    public string? Description;

    public FissionGasEntry(string gas, float amount, float portion, string? description)
    {
        Gas = gas;
        Amount = amount;
        Portion = portion;
        Description = description;
    }
}
