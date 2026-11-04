using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Декор аквариума (/obj/item/aquarium_prop).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialAquariumPropComponent : Component
{
    /// <summary>Состояние в tanks.rsi (в переносном аквариуме — с суффиксом _fish_tank).</summary>
    [DataField]
    public string? State;

    /// <summary>Свой спрайт вместо состояния tanks.rsi (икра луна-рыбы).</summary>
    [DataField]
    public string? Sprite;

    [DataField]
    public ImperialAquariumLayer Layer = ImperialAquariumLayer.Bottom;

    /// <summary>TRAIT_UNIQUE_AQUARIUM_CONTENT: только один такой предмет в аквариуме.</summary>
    [DataField]
    public bool Unique = true;
}
