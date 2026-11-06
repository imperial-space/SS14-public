using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Cosmos;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticStarTouchBeamComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Target;

    [DataField]
    public TimeSpan BeamEndTime;

    [DataField]
    public TimeSpan BeamDuration = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan UpdateTime = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan UpdateDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan UpdateDuration2 = TimeSpan.FromSeconds(30);
}
