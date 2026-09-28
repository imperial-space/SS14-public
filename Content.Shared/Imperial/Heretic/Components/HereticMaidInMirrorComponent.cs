using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMaidInMirrorComponent : Component
{
    [DataField] public float MaxHp = 80f;
    [DataField] public float ExamineHarmCooldown = 10f;
    public Dictionary<EntityUid, TimeSpan> RecentExaminers = new();

    public bool IsInMirrorWorld = false;
    public EntityUid? MirrorBallUid;
    public EntityUid? BeamControllerUid;
}
