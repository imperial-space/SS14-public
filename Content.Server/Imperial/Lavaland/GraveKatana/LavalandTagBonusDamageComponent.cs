using Content.Shared.Damage;

namespace Content.Server.Imperial.Lavaland.GraveKatana;

[RegisterComponent]
public sealed partial class LavalandTagBonusDamageComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier BonusDamage = default!;

    [DataField]
    public string RequiredTag = "LavalandMob";
}
