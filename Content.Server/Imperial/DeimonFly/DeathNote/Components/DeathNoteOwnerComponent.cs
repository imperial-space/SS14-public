namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Серверная связь персонажа со всеми принадлежащими ему тетрадями.
/// Коллекция не позволяет удалению одной тетради сбросить владение остальными.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteOwnerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public readonly HashSet<EntityUid> Notebooks = new();
}

/// <summary>
/// Связь владельца для второго независимого комплекта Тетради смерти.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteOwner2Component : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public readonly HashSet<EntityUid> Notebooks = new();
}

/// <summary>
/// Связь владельца для третьего независимого комплекта Тетради смерти.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteOwner3Component : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public readonly HashSet<EntityUid> Notebooks = new();
}
