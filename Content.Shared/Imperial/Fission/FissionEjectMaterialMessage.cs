using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

[Serializable, NetSerializable]
public sealed class FissionEjectMaterialMessage(string material, int sheets) : BoundUserInterfaceMessage
{
    public readonly string Material = material;
    public readonly int Sheets = sheets;
}
