using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

[Serializable, NetSerializable]
public sealed class ImperialMarketEntry(string id, string item, string name, string description, string category, int price, int stock)
{
    public readonly string Id = id;
    public readonly string Item = item;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly string Category = category;
    public readonly int Price = price;
    public readonly int Stock = stock;
}
