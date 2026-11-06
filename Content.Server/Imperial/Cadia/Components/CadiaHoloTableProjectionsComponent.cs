namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Данные <see cref="CadiaHoloTableSystem"/> для этой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class CadiaHoloTableProjectionsComponent : Component
{
    [ViewVariables]
    public EntityUid Value;
}
