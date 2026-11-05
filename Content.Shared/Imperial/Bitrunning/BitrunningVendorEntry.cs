using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>orderable_item в ui_static_data: цена уже умножена на cargo_cost_multiplier, как в TG.</summary>
[Serializable, NetSerializable]
public sealed class BitrunningVendorEntry(string id, string proto, string name, string description, string category, int cost)
{
    public string Id = id;
    public string Proto = proto;
    public string Name = name;
    public string Description = description;
    public string Category = category;
    public int Cost = cost;
}
