using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Rust;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticRustSmokeComponent : Component
{
    [DataField]
    public float BorgDamage = 200f;
}
