
using Content.Shared.Damage;

namespace Content.Shared.Imperial.Medieval.Artifacts;

[RegisterComponent]
public sealed partial class ArtifactAbilityComponent : Component
{
    public EntityUid OwnerUid { get; set; }
}
[RegisterComponent]
public sealed partial class ArtifactAddDamageOnInitComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new();
}

[RegisterComponent]
public sealed partial class ArtifactIgniteOnDamageComponent : Component
{
    [DataField]
    public float FireStacks = 2;
}
[RegisterComponent]
public sealed partial class ArtifactMultiplyDamageComponent : Component
{
    [DataField]
    public float Multiplier = 1.3f;
}
[RegisterComponent]
public sealed partial class ArtifactPenetrateDamageComponent : Component {}
[RegisterComponent]
public sealed partial class ArtifactActionAddComponent : Component
{
    [DataField]
    public List<string> Actions = new();
    public List<EntityUid> ActionsCreated = new();
}
[RegisterComponent]
public sealed partial class ArtifactVampirismOnHitComponent : Component
{
    [DataField]
    public float Multiplier = 0.2f;
    [DataField]
    public float BloodRestore = 15f;
    [DataField]
    public float AdditionalMultiplier = 0.1f;
}
[RegisterComponent]
public sealed partial class ArtifactHungerHealComponent : Component
{
    [DataField]
    public float Amount = 5f;
}
[RegisterComponent]
public sealed partial class ArtifactLightWeaponComponent : Component
{
    [DataField]
    public float FlashTime = 0.5f;
}
[RegisterComponent]
public sealed partial class ArtifactPoisonComponent : Component
{
    [DataField]
    public float Amount = 2f;
}
[RegisterComponent]
public sealed partial class ArtifactMidasComponent : Component
{
    [DataField]
    public int Amount = 3;
    public List<EntityUid> Blacklist = new();
}
[RegisterComponent]
public sealed partial class ArtifactThrowComponent : Component
{
    [DataField]
    public float Distance = 10f;
}
[RegisterComponent]
public sealed partial class ArtifactExplosionMeleeComponent : Component
{
}
[RegisterComponent]
public sealed partial class ArtifactRangeComponent : Component
{
    [DataField]
    public float Multiplier = 1.7f;
}
[RegisterComponent]
public sealed partial class ArtifactSpeedComponent : Component
{
    [DataField]
    public float Multiplier = 1.1f;
}
[RegisterComponent]
public sealed partial class ArtifactDurabilityComponent : Component
{
    [DataField]
    public float NewWaste = 0f;
}
[RegisterComponent]
public sealed partial class ArtifactStaminaDamageComponent : Component
{
    [DataField]
    public float Damage = 20f;
}
