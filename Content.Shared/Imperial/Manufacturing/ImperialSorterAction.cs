using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

[Serializable, NetSerializable]
public enum ImperialSorterAction : byte
{
    NewFilter,
    DelFilter,
    Rotate,
    RotateUnmet,
    Edit,
    ShiftUp,
    ShiftDown,
}
