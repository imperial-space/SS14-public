using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

[Serializable, NetSerializable]
public enum ImperialModularShieldVisuals : byte
{
    /// <summary>Готовое состояние спрайта (gen_ready_closed, charger_on_open, cable_node_closed_r_nb_l и т. п.).</summary>
    State,

    /// <summary>Прозрачность клетки щита (0..1).</summary>
    Alpha,
}
