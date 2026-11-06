namespace Content.Server.Imperial.TerrorSpider;

/// <summary>
/// Данные <see cref="TerrorSpiderSystem"/> для этой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class TerrorSpiderCocoonStepsComponent : Component
{
    [ViewVariables]
    public int Value;
}
