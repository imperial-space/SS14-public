namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Счётчик культистов, выведенных из строя этим святым оружием (cult_kill_tracker из SS13).
/// Виден только культистам при осмотре.
/// </summary>
[RegisterComponent]
public sealed partial class NullRodCultKillTrackerComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> Slain = new();
}
