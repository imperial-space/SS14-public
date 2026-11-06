namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>
/// Данные <see cref="ImperialMalfAiSystem"/> для этой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialMalfAiApcOverloadComponent : Component
{
    [ViewVariables]
    public int Value;
}
