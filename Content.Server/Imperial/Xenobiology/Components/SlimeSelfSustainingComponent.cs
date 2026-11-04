using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>slimecross/selfsustaining: распадается на четыре автослайма.</summary>
[RegisterComponent]
public sealed partial class SlimeSelfSustainingComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Extract;

    [DataField]
    public EntProtoId Autoslime = "SlimeAutoslime";
}
