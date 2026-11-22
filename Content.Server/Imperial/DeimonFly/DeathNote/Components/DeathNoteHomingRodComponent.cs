namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Удерживает стержень Тетради смерти на назначенной жертве без изменения штатной реализации стержня.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteHomingRodComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public float Speed;

    [ViewVariables]
    public float TurnResponsiveness;

    [ViewVariables]
    public float ImpactRadius;
}
