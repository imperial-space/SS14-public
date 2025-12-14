using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class DamageOnContactComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage;
}
