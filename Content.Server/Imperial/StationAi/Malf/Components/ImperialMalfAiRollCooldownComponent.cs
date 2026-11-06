namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>
/// Данные <see cref="ImperialMalfAiSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ImperialMalfAiRollCooldownComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
