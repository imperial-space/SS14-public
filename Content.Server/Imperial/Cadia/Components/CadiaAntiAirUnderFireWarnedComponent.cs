namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Данные <see cref="CadiaAntiAirSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CadiaAntiAirUnderFireWarnedComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
