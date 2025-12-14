using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Serialization;

namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class BloodlettingToolComponent : Component
{
    [DataField]
    public float Duration = 15;

    [DataField]
    public DamageSpecifier Damage = new();

    public BloodlettingResult Result = BloodlettingResult.None;

    public DoAfterId? DoAfter;
}

