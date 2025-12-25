namespace Content.Shared.Imperial.Medieval.Revive;

/// <summary>
/// Если уничтожить (именно разрушить через Destructible) сущность с этим компонентом
/// то к счетчику убийств игрока добавится 1
/// </summary>
[RegisterComponent]
public sealed partial class KillReviveGoalComponent : Component
{
}
