using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

[RegisterComponent, NetworkedComponent]
public sealed partial class WerewolfHotBloodComponent : Component
{
    public float Modifier = 1.3f;
}
