namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Связывает начатые эффекты с последующей фактической смертью цели в ограниченном окне причинности.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteTargetTrackingComponent : Component
{
    [ViewVariables]
    public Dictionary<uint, TimeSpan> EntryDeadlines = new();

    [ViewVariables]
    public HashSet<uint> UnrevivableEntryIds = new();

    [ViewVariables]
    public bool OwnsUnrevivableComponent;

    [ViewVariables]
    public TimeSpan NextExpiry;
}
