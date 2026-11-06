using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.PrisonCube;

[RegisterComponent]
[Access(typeof(PrisonCubeTeleportSystem))]
public sealed partial class PrisonCubePairSpawnerComponent : Component
{
    [DataField]
    public EntProtoId RedPrototype = "PrisonCubeTeleportRed";

    [DataField]
    public EntProtoId BluePrototype = "PrisonCubeTeleportBlue";
}
