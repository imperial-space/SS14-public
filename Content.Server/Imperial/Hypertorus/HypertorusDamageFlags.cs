using Content.Shared.Atmos;
using Content.Shared.Imperial.Hypertorus;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Hypertorus;

/// <summary>warning_damage_flags: что именно разрушает гиперторус.</summary>
[Flags]
public enum HypertorusDamageFlags
{
    None = 0,
    HighPowerDamage = 1 << 0,
    HighFuelMixMole = 1 << 1,
    IronContentDamage = 1 << 2,
    IronContentIncrease = 1 << 3,
    Emped = 1 << 4,
}
