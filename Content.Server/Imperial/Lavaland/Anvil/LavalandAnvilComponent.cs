using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.Imperial.Lavaland.Anvil;

[RegisterComponent]
public sealed partial class LavalandAnvilComponent : Component
{
    [DataField]
    public string ContainerId = "lavaland_anvil";

    [DataField]
    public int MaxCharges = 2;

    [DataField]
    public int CurrentCharges = 2;

    /// <summary>Seconds per charge recharge.</summary>
    [DataField]
    public float RechargeTime = 300f;

    public float RechargeTimer = 0f;
}
