namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Данные <see cref="CadiaSquadSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CadiaSquadVoxCooldownComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
