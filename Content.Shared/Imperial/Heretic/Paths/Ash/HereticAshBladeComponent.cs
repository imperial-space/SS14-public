using Content.Shared.Damage;

namespace Content.Shared.Imperial.Heretic.Paths.Ash;

[RegisterComponent]
public sealed partial class HereticAshBladeComponent : Component
{
    [DataField]
    public DamageSpecifier BonusDamage = new() { DamageDict = { ["Stamina"] = 10, ["Heat"] = 10 } };

    [DataField]
    public float AshBladeMeleeHitRange = 5f;
}
