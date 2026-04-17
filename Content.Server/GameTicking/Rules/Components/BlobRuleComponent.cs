using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Imperial.Blob;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(BlobRuleSystem))]
public sealed partial class BlobRuleComponent : Component
{
    [DataField]
    public EntProtoId OvermindPrototype = "MobBlobOvermind";

    [DataField]
    public EntProtoId CorePrototype = "BlobCore";

    [DataField]
    public EntProtoId MousePrototype = "MobBlobMouse";

    [DataField]
    public EntProtoId GhostRoleSpawnerPrototype = "SpawnPointGhostBlobMouse";

    [DataField]
    public SoundSpecifier? GreetSoundNotification;

    [DataField]
    public BlobChemicalType StartingChemical = BlobChemicalType.Sorium;

    [DataField]
    public bool StartAsCarrier = true;

    [DataField]
    public bool SpawnGhostRoleAtVentOnStart;

    [DataField]
    public int WinningInfectedStationTiles = 350;

    [DataField]
    public int GammaThreshold = 250;

    [DataField]
    public float BiohazardAnnouncementDelay = 180f;

    [DataField]
    public float VictoryCheckInterval = 5f;

    [ViewVariables]
    public float BiohazardAnnouncementAccumulator;

    [ViewVariables]
    public bool BiohazardAnnouncementSent;

    [ViewVariables]
    public bool GammaTriggered;

    [ViewVariables]
    public float VictoryAccumulator;

    [ViewVariables]
    public bool VictoryTriggered;

    [ViewVariables]
    public readonly List<EntityUid> BlobMinds = new();
}