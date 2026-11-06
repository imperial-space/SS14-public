using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Rust;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticRustSmokeComponent : Component
{
    [DataField]
    public float BorgDamage = 200f;

    [DataField]
    public float MapInitRange = 0.4f;

    [DataField]
    public float MapInitRange2 = 0.6f;
}
