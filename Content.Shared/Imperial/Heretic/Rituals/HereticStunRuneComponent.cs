using Content.Shared.Damage;

namespace Content.Shared.Imperial.Heretic.Rituals;

[RegisterComponent]
public sealed partial class HereticStunRuneComponent : Component
{
    [DataField]
    public DamageSpecifier TriggeredBluntDamage = new() { DamageDict = { ["Blunt"] = 15 } };

    [DataField]
    public TimeSpan TriggeredDuration = TimeSpan.FromSeconds(5);
}
