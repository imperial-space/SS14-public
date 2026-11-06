namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Данные <see cref="CadiaFlightSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CadiaFlightNextDenyPopupComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
