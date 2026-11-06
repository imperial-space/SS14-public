namespace Content.Server.Imperial.Changeling;

/// <summary>
/// Данные <see cref="ImperialChangelingSystem"/> для этой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialChangelingOldColdThresholdComponent : Component
{
    [ViewVariables]
    public float Value;
}
