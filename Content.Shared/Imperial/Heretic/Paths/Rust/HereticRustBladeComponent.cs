namespace Content.Shared.Imperial.Heretic.Paths.Rust;

[RegisterComponent]
public sealed partial class HereticRustBladeComponent : Component
{
    [DataField]
    public TimeSpan RustBladeMeleeHitTime = TimeSpan.FromSeconds(3);
}
