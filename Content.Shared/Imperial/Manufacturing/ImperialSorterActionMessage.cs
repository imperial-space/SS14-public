using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

[Serializable, NetSerializable]
public sealed class ImperialSorterActionMessage(ImperialSorterAction action, int id = -1) : BoundUserInterfaceMessage
{
    public readonly ImperialSorterAction Action = action;
    public readonly int Id = id;
}
