using Robust.Shared.GameStates;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(true, true)]
public sealed partial class CursedMarkComponent : Component
{
    [DataField]
    public float Delay = 6f;

    [DataField]
    public Color ActiveColor = Color.OrangeRed;

    [DataField]
    public Color InactiveColor = Color.Gray;

    [AutoNetworkedField]
    public NetEntity NetEntity = NetEntity.Invalid;

    public EntityUid FlameEntity = EntityUid.Invalid;

    public TimeSpan ExplodeTime = TimeSpan.Zero;
}
