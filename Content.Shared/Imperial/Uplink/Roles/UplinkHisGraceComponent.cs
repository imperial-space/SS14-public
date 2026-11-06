using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Его Милость.</summary>
[RegisterComponent]
public sealed partial class UplinkHisGraceComponent : Component
{
    [ViewVariables]
    public bool Awakened;

    [ViewVariables]
    public float Bloodthirst;

    [ViewVariables]
    public float PrevBloodthirst;

    [ViewVariables]
    public float ForceBonus;

    [ViewVariables]
    public int Victims;

    [ViewVariables]
    public bool Ascended;

    [ViewVariables]
    public TimeSpan NextTick;

    [DataField]
    public float BaseForce = 12f;

    [DataField]
    public DamageSpecifier UpdateHisGraceDamage = new() { DamageDict = { ["Blunt"] = 100 } };

    [DataField]
    public DamageSpecifier ConsumeDamage = new() { DamageDict = { ["Blunt"] = 200 } };

    [DataField]
    public TimeSpan UpdateHisGraceDuration = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float UpdateHisGraceRange = 2f;

    [DataField]
    public float UpdateHisGraceAmount = -5f;
}
