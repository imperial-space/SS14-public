using Content.Shared.Damage;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Плавно удерживает штатный метеор на назначенной цели до первого попадания.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteHomingMeteorComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public float Speed;

    [ViewVariables]
    public float TurnResponsiveness;

    [ViewVariables]
    public float ImpactRadius;

    [ViewVariables]
    public DamageSpecifier Damage = new();
}
