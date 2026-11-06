using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

/// <summary>Положение камеры стержня (CHAMBER_* из SS220 NGCR).</summary>
[Serializable, NetSerializable]
public enum FissionChamberState : byte
{
    Down = 1,
    Up = 2,
    Open = 3,
    OverloadIdle = 4,
    OverloadActive = 5,
}
