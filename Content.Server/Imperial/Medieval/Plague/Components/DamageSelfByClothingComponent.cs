using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class DamageSelfByClothingComponent : Component
{
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField]
    public DamageSpecifier Damage = new();
}
