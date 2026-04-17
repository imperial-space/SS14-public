using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Выдаётся культисту при надевании культового доспеха.
/// Блокирует входящий урон от враждебных существ: первые <see cref="MaxCharges"/> ударов.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CultShieldComponent : Component
{
    /// <summary>Текущее количество зарядов щита.</summary>
    [DataField, AutoNetworkedField]
    public int Charges = 3;

    /// <summary>Максимальное количество зарядов (восстанавливается через Кровавый Обряд).</summary>
    [DataField]
    public int MaxCharges = 3;

    /// <summary>Server-only: сущность визуального эффекта щита (спрайт).</summary>
    [ViewVariables]
    public EntityUid? VisualEntity;
}
