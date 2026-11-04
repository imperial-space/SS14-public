using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Генная пушка для рыб (/obj/item/fish_genegun): пустой шприц извлекает черту, инъектор вводит её.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishGenegunComponent : Component
{
    public const string SlotId = "genegun_injector";
}
