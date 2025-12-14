using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

[RegisterComponent, NetworkedComponent]
public sealed partial class WerewolfMoonRageComponent : Component
{
    public float Modifier = 1.1f;
}
