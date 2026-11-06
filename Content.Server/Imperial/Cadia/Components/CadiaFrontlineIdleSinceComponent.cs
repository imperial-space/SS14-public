namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Данные <see cref="CadiaFrontlineSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CadiaFrontlineIdleSinceComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
