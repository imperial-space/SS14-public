using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Обратный медвежий капкан.</summary>
[RegisterComponent]
public sealed partial class UplinkReverseBearTrapComponent : Component
{
    [DataField]
    public TimeSpan Countdown = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan? KillAt;

    [ViewVariables]
    public int EscapeChance;

    [ViewVariables]
    public TimeSpan NextBeep;

    [DataField]
    public DamageSpecifier SnapTrapDamage = new() { DamageDict = { ["Slash"] = 400 } };

    [DataField]
    public TimeSpan BearTrapInteractDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan BearTrapVerbsDelay = TimeSpan.FromSeconds(2);
}
