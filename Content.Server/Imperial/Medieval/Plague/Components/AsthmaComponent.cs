using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class AsthmaComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new();
}
