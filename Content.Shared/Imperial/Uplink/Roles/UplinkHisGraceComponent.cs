using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

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
}
