using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;

[RegisterComponent]
public sealed partial class RandomCreationsPaintingComponent : Component
{
    [DataField]
    public EntProtoId PaintingPrototype = "Canvas";
}
