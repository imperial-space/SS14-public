namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Данные <see cref="CadiaVehicleSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CadiaVehicleNextRunOverComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
