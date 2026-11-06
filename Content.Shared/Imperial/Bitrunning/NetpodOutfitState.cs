using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>NetpodOutfits: коллекция «Профессии» и выбранный netsuit.</summary>
[Serializable, NetSerializable]
public sealed class NetpodOutfitState(string? selected, List<NetpodOutfitEntry> jobs) : BoundUserInterfaceState
{
    public string? Selected = selected;
    public List<NetpodOutfitEntry> Jobs = jobs;
}
