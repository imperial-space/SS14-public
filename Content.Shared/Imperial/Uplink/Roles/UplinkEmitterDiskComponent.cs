using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Диск гиперконтузионного диода для эмиттера.</summary>
[RegisterComponent]
public sealed partial class UplinkEmitterDiskComponent : Component
{
    [DataField]
    public EntProtoId Bolt = "ImperialEmitterBoltBlast";

    /// <summary>Во сколько раз реже стреляет эмиттер (fire_rate_mod у TG; гиперконтузионный — 2).</summary>
    [DataField]
    public float FireIntervalMultiplier = 2f;
}
