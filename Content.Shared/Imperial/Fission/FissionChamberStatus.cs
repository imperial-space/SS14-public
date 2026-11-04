using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

/// <summary>Цветной индикатор камеры (red/orange/green/blue/overload).</summary>
[Serializable, NetSerializable]
public enum FissionChamberStatus : byte
{
    None,
    Red,
    Orange,
    Green,
    Blue,
    Overload,
}
