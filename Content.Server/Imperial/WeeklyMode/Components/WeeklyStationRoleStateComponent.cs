namespace Content.Server.WeeklyMode.Components;

[RegisterComponent]
public sealed partial class WeeklyStationRoleStateComponent : Component
{
    public readonly Dictionary<string, int?> OriginalSlots = new();
}
