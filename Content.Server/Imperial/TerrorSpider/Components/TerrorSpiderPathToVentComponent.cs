namespace Content.Server.Imperial.TerrorSpider;

/// <summary>
/// Данные <see cref="TerrorSpiderSystem"/> для этой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class TerrorSpiderPathToVentComponent : Component
{
    [ViewVariables]
    public (EntityUid Vent, int Steps) Value;
}
