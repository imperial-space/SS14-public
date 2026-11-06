using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

// ── Чёрный рынок (code/modules/cargo/markets) ──

/// <summary>/datum/market_item: товар чёрного рынка.</summary>
[Prototype]
public sealed partial class ImperialMarketItemPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Item;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public string Category = string.Empty;

    [DataField]
    public int PriceMin;

    [DataField]
    public int PriceMax;

    [DataField]
    public int StockMin = 1;

    [DataField]
    public int StockMax = 1;

    /// <summary>availability_prob, %.</summary>
    [DataField]
    public int Availability = 100;
}
