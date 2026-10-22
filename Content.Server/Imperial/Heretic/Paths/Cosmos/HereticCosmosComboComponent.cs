namespace Content.Server.Imperial.Heretic.Paths.Cosmos;

/// <summary>
/// Комбо клинка Космоса: удары по разным целям подряд наносят всё больше урона.
/// </summary>
[RegisterComponent, Access(typeof(HereticCosmosActionsSystem))]
public sealed partial class HereticCosmosComboComponent : Component
{
    [ViewVariables]
    public EntityUid FirstTarget = EntityUid.Invalid;

    [ViewVariables]
    public EntityUid SecondTarget = EntityUid.Invalid;

    [ViewVariables]
    public int ComboCount;

    /// <summary>После этого момента комбо начинается заново.</summary>
    [ViewVariables]
    public TimeSpan ResetAt;

    /// <summary>Сколько времени есть на следующий удар комбо.</summary>
    [DataField]
    public TimeSpan ComboWindow = TimeSpan.FromSeconds(3);
}
