using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticRustWalkerComponent : Component
{
    [DataField] public float HealInterval = 1f;
    [DataField] public float HealAmount   = 3f;

    public float HealAccum;
}
