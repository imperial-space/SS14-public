using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>req_stat: наихудшее состояние, в котором способность ещё работает (CONSCIOUS, HARD_CRIT, DEAD).</summary>
public enum ImperialChangelingStat : byte
{
    Conscious,
    HardCrit,
    Dead,
}
