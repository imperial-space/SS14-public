using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Обратный револьвер: у не-клоуна стреляет в стрелка.</summary>
[RegisterComponent]
public sealed partial class UplinkReverseGunComponent : Component
{
    [DataField]
    public float Damage = 60f;
}
