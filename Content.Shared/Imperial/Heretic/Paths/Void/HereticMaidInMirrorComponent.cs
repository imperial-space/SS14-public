using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Void;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMaidInMirrorComponent : Component
{
    [DataField] public float MaxHp = 80f;
    [DataField] public TimeSpan ExamineHarmCooldown = TimeSpan.FromSeconds(10f);
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> RecentExaminers = new();

    [ViewVariables]
    public bool IsInMirrorWorld = false;
    [ViewVariables]
    public EntityUid? MirrorBallUid;
    [ViewVariables]
    public EntityUid? BeamControllerUid;

    [DataField]
    public TimeSpan PhaseActionDelay = TimeSpan.FromSeconds(3f);
}
