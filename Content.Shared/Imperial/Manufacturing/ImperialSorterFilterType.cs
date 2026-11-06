using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

/// <summary>Фильтр сортировщика (/datum/sortrouter_filter/*).</summary>
[Serializable, NetSerializable]
public enum ImperialSorterFilterType : byte
{
    IsStack,
    IsOre,
    IsMail,
    NameContains,
    SpecificItem,
    SpecificKind,
}
