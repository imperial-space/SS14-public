using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Одержимый клинок: использование в руке зовёт призрака, который вселяется в клинок и может говорить.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NullRodSpiritHoldingComponent : Component
{
    /// <summary>Призыв уже идёт или дух уже вселился.</summary>
    [DataField, AutoNetworkedField]
    public bool Awakening;

    /// <summary>Компоненты, которые добавляются клинку при пробуждении (роль призрака).</summary>
    [DataField(required: true)]
    public ComponentRegistry AwakenComponents = new();
}
