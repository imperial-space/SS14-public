using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.Imperial.Lavaland.Anvil;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class TemporaryUpgradeComponent : Component
{
    /// <summary>Prototype ID to spawn when upgrade expires.</summary>
    [DataField]
    public string RevertProto = string.Empty;

    /// <summary>Длительность улучшения.</summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(20);

    /// <summary>Когда улучшение спадёт.</summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan ExpireAt;
}
