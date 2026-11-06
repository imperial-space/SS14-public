using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Банка корма для рыб (/obj/item/reagent_containers/cup/fish_feed).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishFeedComponent : Component;
