using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Мясной крюк: снаряд притягивает цель к стрелку.</summary>
[RegisterComponent]
public sealed partial class UplinkHookProjectileComponent : Component
{
    [DataField]
    public float HookHitBaseThrowSpeed = 10f;
}
