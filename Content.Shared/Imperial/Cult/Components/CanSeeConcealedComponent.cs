using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Даёт сущности возможность видеть скрытые руны и структуры культа.
/// Добавляется: при надевании повязки фанатика.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CanSeeConcealedComponent : Component
{
}
