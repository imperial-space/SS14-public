using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class ReagentAllergyComponent : Component
{
    [DataField]
    public List<string> Reagents = new();

    [DataField]
    public List<string> RandomReagents = new();

    [DataField]
    public int RandomCount = 3;

    [DataField]
    public DamageSpecifier Damage = new(new() { DamageDict = new() { { "Poison", 4 } } });
}
