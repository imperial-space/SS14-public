using Content.Shared.Damage;

namespace Content.Shared.Imperial.Heretic.Paths.Flesh;

[RegisterComponent]
public sealed partial class HereticFleshPassiveComponent : Component
{
    [DataField]
    public DamageSpecifier IngestingHeal = new() { DamageDict = { ["Blunt"] = -15, ["Slash"] = -10, ["Piercing"] = -5 } };
}
