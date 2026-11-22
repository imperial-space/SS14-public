namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Серверная связь персонажа со всеми тетрадями, находящимися прямо в его руках.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteHolderComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public readonly HashSet<EntityUid> Notebooks = new();
}

/// <summary>
/// Временная связь держателя для второго независимого комплекта Тетради смерти.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteHolder2Component : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public readonly HashSet<EntityUid> Notebooks = new();
}

/// <summary>
/// Временная связь держателя для третьего независимого комплекта Тетради смерти.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteHolder3Component : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public readonly HashSet<EntityUid> Notebooks = new();
}
