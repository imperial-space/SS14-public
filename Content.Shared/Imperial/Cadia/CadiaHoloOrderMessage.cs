using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Приказ отряду с гололитического стола: точка на карте этажа.</summary>
[Serializable, NetSerializable]
public sealed class CadiaHoloOrderMessage(int squad, CadiaSquadOrder order, Vector2 point, CadiaFlightLevel level) : BoundUserInterfaceMessage
{
    public readonly int Squad = squad;
    public readonly CadiaSquadOrder Order = order;
    public readonly Vector2 Point = point;
    public readonly CadiaFlightLevel Level = level;
}
