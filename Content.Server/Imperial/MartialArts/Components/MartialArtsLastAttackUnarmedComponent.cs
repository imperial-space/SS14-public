namespace Content.Server.Imperial.MartialArts;

/// <summary>
/// Данные <see cref="MartialArtsSystem"/> для этой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class MartialArtsLastAttackUnarmedComponent : Component
{
    [ViewVariables]
    public bool Value;
}
