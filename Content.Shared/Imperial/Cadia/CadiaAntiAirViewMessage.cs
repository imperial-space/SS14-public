using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Навести камеру наводчика на борт (null — вернуть камеру в бункер).</summary>
[Serializable, NetSerializable]
public sealed class CadiaAntiAirViewMessage(NetEntity? grid) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Grid = grid;
}
