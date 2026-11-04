using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>liked/disliked/toxic_foodtypes языка вида TG. id — вид SS14.</summary>
[Prototype]
public sealed partial class ImperialSpeciesTastePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ImperialFoodTypes> Liked = new();

    [DataField]
    public List<ImperialFoodTypes> Disliked = new();

    [DataField]
    public List<ImperialFoodTypes> Toxic = new();
}
