using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>barriercube: превращается в баррикаду.</summary>
[RegisterComponent]
public sealed partial class SlimeBarrierCubeComponent : Component
{
    [DataField]
    public EntProtoId Barrier = "SlimeBarricade";

    [DataField]
    public float BarrierCubeUseRange = 0.4f;
}
