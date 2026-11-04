using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishCatalogBoundUserInterfaceState(List<ImperialFishCatalogEntry> entries) : BoundUserInterfaceState
{
    public readonly List<ImperialFishCatalogEntry> Entries = entries;
}
