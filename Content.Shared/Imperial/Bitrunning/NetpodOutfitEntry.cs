using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

[Serializable, NetSerializable]
public sealed class NetpodOutfitEntry(string id, string name)
{
    public string Id = id;
    public string Name = name;
}
