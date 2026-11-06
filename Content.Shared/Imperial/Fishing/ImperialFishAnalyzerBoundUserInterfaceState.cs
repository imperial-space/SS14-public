using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishAnalyzerBoundUserInterfaceState(List<ImperialFishAnalyzerEntry> entries, bool fishScanned) : BoundUserInterfaceState
{
    public readonly List<ImperialFishAnalyzerEntry> Entries = entries;

    /// <summary>Сканировали одну рыбу (а не аквариум): маленькое окно без поиска.</summary>
    public readonly bool FishScanned = fishScanned;
}
