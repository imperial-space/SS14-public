namespace Content.Shared.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Маркер административной тетради только для чтения и её безопасные настройки пагинации.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteAdminLedgerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int EntriesPerPage = 20;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan InvalidAttemptLogCooldown = TimeSpan.FromSeconds(5);
}
