namespace Content.Server.Imperial.SyndieBattle;

/// <summary>
/// This is used for syndie battles respawn touch
/// </summary>
[RegisterComponent]
public sealed partial class RespawnTouchComponent : Component
{
    /// <summary>
    /// Удалить тело после респавна?
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool DeleteBody = true;
}
