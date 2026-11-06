namespace Content.Server.Imperial.Morph;

/// <summary>
/// Данные <see cref="MorphSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class MorphNextAiDisguiseComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
