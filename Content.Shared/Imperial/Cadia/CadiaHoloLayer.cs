using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public enum CadiaHoloLayer : byte
{
    Assault,
    Defense,
    Intel,
}
