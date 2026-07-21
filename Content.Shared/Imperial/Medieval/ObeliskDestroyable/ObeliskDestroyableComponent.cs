using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;

namespace Content.Shared.Imperial.Medieval.ObeliskDestroyable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ObeliskDestroyableComponent : Component
{
    [DataField(required: true)]
    public List<ObeliskDestroyablePhaseData> Phases = new();

    [DataField]
    public ProtoId<DepartmentPrototype> LockedDepartment = string.Empty;

    [DataField]
    public ProtoId<MedievalFactionPrototype> Faction = "Legion";

    [DataField]
    public TimeSpan InvincibilityDuration = TimeSpan.FromMinutes(5);

    [DataField]
    public LocId AnnouncementSender = "obelisk-destructable-announcer";

    [DataField]
    public string AnnouncementLocPrefix = "obelisk-destructable-phase";

    [DataField]
    public Color AnnouncementColor = Color.OrangeRed;

    [DataField]
    public SoundSpecifier AnnouncementSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/horn.ogg");

    [DataField]
    public EntProtoId? DestroyedEffect;

    [DataField]
    public LocId DestroyedDescription = "medieval-faction-obelisk-destroyed-desc";

    [DataField(required: true)]
    public List<string> BaseLayerStates = new();

    [DataField(required: true)]
    public string OuterLayerState = string.Empty;

    [DataField]
    public string InvincibleOuterLayerState = "invincible";

    [AutoNetworkedField]
    public int CurrentPhase;

    [AutoNetworkedField]
    public bool InvincibilityActive;
}

[DataDefinition]
public sealed partial class ObeliskDestroyablePhaseData
{
    [DataField(required: true)]
    public FixedPoint2 Threshold = FixedPoint2.Zero;

    [DataField(required: true)]
    public LocId Announcement = string.Empty;

    [DataField]
    public bool DestroyOnReached;
}
