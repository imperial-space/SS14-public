using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

[Serializable, NetSerializable]
public sealed class ImperialSorterState(List<ImperialSorterFilterData> filters, Direction unmet) : BoundUserInterfaceState
{
    public readonly List<ImperialSorterFilterData> Filters = filters;
    public readonly Direction UnmetDir = unmet;
}
