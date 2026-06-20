namespace Content.Shared.Imperial.Lavaland.Weapons;

/// <summary>
/// Applies extra control effects when a meat-hook projectile lands on a target.
/// </summary>
[RegisterComponent]
public sealed partial class MeatHookProjectileComponent : Component
{
    [DataField]
    public float PullImpulse = 22f;

    [DataField]
    public float StunSeconds = 3f;
}
