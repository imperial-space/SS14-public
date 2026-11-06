namespace Content.Server.Imperial.Changeling;

/// <summary>
/// Данные <see cref="ImperialChangelingSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ImperialChangelingLastResortConfirmComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
