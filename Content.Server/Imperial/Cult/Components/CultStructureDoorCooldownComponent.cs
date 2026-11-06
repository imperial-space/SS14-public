namespace Content.Server.Imperial.Cult;

/// <summary>
/// Данные <see cref="CultStructureSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CultStructureDoorCooldownComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
