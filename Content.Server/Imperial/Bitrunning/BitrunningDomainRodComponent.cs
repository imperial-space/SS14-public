using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Удочка домена с очками за рыбу (breeze_bay/on_fish_caught).</summary>
[RegisterComponent]
public sealed partial class BitrunningDomainRodComponent : Component
{
    [ViewVariables]
    public EntityUid Server;

    [ViewVariables]
    public int Points;
}
