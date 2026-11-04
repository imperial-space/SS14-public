using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// gun/energy/recharge/kinetic_accelerator: самозаряжающийся акселератор с модулями (max_mod_capacity).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KineticAcceleratorComponent : Component
{
    /// <summary>max_mod_capacity.</summary>
    [DataField]
    public int MaxModCapacity = 100;

    [DataField]
    public string ContainerId = "mining_upgrades";

    /// <summary>recharge_time без модулей, секунды.</summary>
    [DataField]
    public float RechargeTime = 1.6f;

    /// <summary>projectile/kinetic range, клетки.</summary>
    [DataField]
    public int Range = 3;

    [DataField]
    public SoundSpecifier? InsertSound = new SoundPathSpecifier("/Audio/Items/screwdriver.ogg");
}
