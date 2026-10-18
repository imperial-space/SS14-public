using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.PrisonCube;

[RegisterComponent]
[Access(typeof(PrisonCubeTeleportSystem))]
public sealed partial class PrisonCubeTeleportComponent : Component
{
    [ViewVariables]
    public EntityUid? Partner;

    [DataField]
    public EntProtoId SmokePrototype = "PrisonCubeTeleportSmoke";
}
