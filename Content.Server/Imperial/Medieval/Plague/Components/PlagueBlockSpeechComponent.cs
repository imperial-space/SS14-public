using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class PlagueBlockSpeechComponent : Component
{
    [DataField]
    public float Chance = 1f;

    [DataField]
    public DamageSpecifier Damage = new();
}
