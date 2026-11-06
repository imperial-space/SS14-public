namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Данные <see cref="CadiaWorldBoundsSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CadiaWorldBoundsNextPopupComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
