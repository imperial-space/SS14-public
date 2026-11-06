namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Данные <see cref="CadiaPylonVeilSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CadiaPylonVeilNextPopupComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
