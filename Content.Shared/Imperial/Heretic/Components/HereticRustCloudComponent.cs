using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticRustSmokeComponent : Component
{
    [DataField]
    public float BorgDamage = 200f;
}
