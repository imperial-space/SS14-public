using System.Linq;
using Content.Shared.Atmos;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Hypertorus;

/// <summary>Флаги аварии гиперторуса (HYPERTORUS_FLAG_* в _hfr_defines.dm).</summary>
[Flags]
public enum HypertorusMeltdownFlags
{
    None = 0,
    BaseExplosion = 1 << 0,
    MediumExplosion = 1 << 1,
    DevastatingExplosion = 1 << 2,
    RadiationPulse = 1 << 3,
    Emp = 1 << 4,
    MinimumSpread = 1 << 5,
    MediumSpread = 1 << 6,
    BigSpread = 1 << 7,
    MassiveSpread = 1 << 8,
    CriticalMeltdown = 1 << 9,
}
