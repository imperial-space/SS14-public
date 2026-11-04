using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

[Serializable, NetSerializable]
public sealed class FissionMaterialEntry
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public int Amount;
    public int Sheets;
}
