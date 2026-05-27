using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.PrisonCube;

[RegisterComponent]
[Access(typeof(PrisonCubeTeleportSystem))]
public sealed partial class PrisonCubeTeleportComponent : Component
{
    [DataField(required: true)]
    public string LinkChannel = default!;

    [DataField(required: true)]
    public string TargetChannel = default!;

    [DataField]
    public EntProtoId SmokePrototype = "PrisonCubeTeleportSmoke";
}
