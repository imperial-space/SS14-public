using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

[Serializable, NetSerializable]
public sealed class FissionFabricatorUiState : BoundUserInterfaceState
{
    public List<FissionRodDesign> Designs = new();
    public List<FissionMaterialEntry> Resources = new();
}
