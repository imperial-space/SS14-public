using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Ледяной блок frozenstasis / ice_stasis.</summary>
[RegisterComponent]
public sealed partial class SlimeIceStasisComponent : Component
{
    [ViewVariables]
    public EntityUid? Frozen;
}
