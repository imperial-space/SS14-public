using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Uplink.Weapons;

/// <summary>
/// Силовой кулак: газовый поршень добавляет урон и отбрасывает цель.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PowerFistComponent : Component
{
    [DataField]
    public string TankSlot = "gas_tank";

    /// <summary>1 — низкое, 2 — среднее, 3 — высокое давление.</summary>
    [DataField, AutoNetworkedField]
    public int Pressure = 1;

    /// <summary>gas_per_fist — молей за удар на единицу давления.</summary>
    [DataField]
    public float GasPerFist = 3f;

    /// <summary>force кулака.</summary>
    [DataField]
    public float Force = 20f;
}
