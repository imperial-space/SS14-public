using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MartialArts;

/// <summary>GRAB_PASSIVE … GRAB_KILL.</summary>
[Serializable, NetSerializable]
public enum MartialGrabState : byte
{
    Passive = 0,
    Aggressive = 1,
    Neck = 2,
    Kill = 3,
}
