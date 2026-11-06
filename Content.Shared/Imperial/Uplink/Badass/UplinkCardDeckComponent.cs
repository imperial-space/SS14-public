using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>Карты Синдиката (toy/cards/deck/syndicate): колода, из которой тянут острые карты.</summary>
[RegisterComponent]
public sealed partial class UplinkCardDeckComponent : Component
{
    [DataField]
    public EntProtoId Card = "ImperialSyndicateCard";

    [DataField]
    public int Cards = 52;

    [DataField]
    public int MaxCards = 52;
}
