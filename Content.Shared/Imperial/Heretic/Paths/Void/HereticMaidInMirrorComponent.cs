using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Void;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMaidInMirrorComponent : Component
{
    [DataField] public float MaxHp = 80f;
    [DataField] public float ExamineHarmCooldown = 10f;
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> RecentExaminers = new();

    [ViewVariables]
    public bool IsInMirrorWorld = false;
    [ViewVariables]
    public EntityUid? MirrorBallUid;
    [ViewVariables]
    public EntityUid? BeamControllerUid;
}
