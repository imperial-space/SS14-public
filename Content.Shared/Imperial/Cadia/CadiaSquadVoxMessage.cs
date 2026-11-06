using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed class CadiaSquadVoxMessage(int number, CadiaVoxCommand command) : BoundUserInterfaceMessage
{
    public readonly int Number = number;
    public readonly CadiaVoxCommand Command = command;
}
