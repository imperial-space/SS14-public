using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed class CadiaHoloRequestTilesMessage(CadiaFlightLevel level) : BoundUserInterfaceMessage
{
    public readonly CadiaFlightLevel Level = level;
}
