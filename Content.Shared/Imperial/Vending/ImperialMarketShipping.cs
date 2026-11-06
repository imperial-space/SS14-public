using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

[Serializable, NetSerializable]
public enum ImperialMarketShipping : byte
{
    /// <summary>SHIPPING_METHOD_LAUNCH: бесплатно, падает где-то рядом.</summary>
    Launch,

    /// <summary>SHIPPING_METHOD_TELEPORT: телепорт к покупателю.</summary>
    Teleport,
}
