using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Энциклопедия рыб (/obj/item/book/manual/fish_catalog).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishCatalogComponent : Component;
