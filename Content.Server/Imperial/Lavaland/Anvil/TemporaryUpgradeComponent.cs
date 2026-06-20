using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.Imperial.Lavaland.Anvil;

[RegisterComponent]
public sealed partial class TemporaryUpgradeComponent : Component
{
    /// <summary>Prototype ID to spawn when upgrade expires.</summary>
    [DataField]
    public string RevertProto = string.Empty;

    /// <summary>Total upgrade duration in seconds.</summary>
    [DataField]
    public float Duration = 1200f; // 20 minutes

    public float TimeRemaining;
}
