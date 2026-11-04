using System.Linq;
using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Security;

/// <summary>mod/module/energy_shield/gamma: три заряда, гасящих любую атаку.</summary>
[RegisterComponent]
public sealed partial class GammaEnergyShieldComponent : Component
{
    [DataField]
    public int MaxCharges = 3;

    /// <summary>shield_icon: «shield» — гамма, «shield-red» — Синдикат.</summary>
    [DataField]
    public string ShieldState = "shield";

    [ViewVariables]
    public int Charges = 3;

    /// <summary>recharge_start_delay.</summary>
    [DataField]
    public TimeSpan RechargeStartDelay = TimeSpan.FromSeconds(20);

    /// <summary>charge_increment_delay.</summary>
    [DataField]
    public TimeSpan ChargeIncrementDelay = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextRecharge;

    [ViewVariables]
    public EntityUid? Wearer;
}
