namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Данные <see cref="ImperialFishTraitSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ImperialFishTraitInkCooldownComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
