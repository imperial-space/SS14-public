namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Отмечает призванное существо, чей обычный ближний урон игнорирует защиту только одной цели Тетради смерти.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNotePriorityTargetComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public TimeSpan NextRetargetAt;

    [ViewVariables]
    public TimeSpan RetargetInterval;
}
