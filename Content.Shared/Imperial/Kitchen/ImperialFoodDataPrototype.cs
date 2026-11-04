using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>crafting_complexity и foodtypes блюда (/obj/item/food TG). id — прототип сущности.</summary>
[Prototype]
public sealed partial class ImperialFoodDataPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public int Complexity;

    [DataField]
    public List<ImperialFoodTypes> Types = new();

    /// <summary>bakeable для существующих блюд SS14 (у новых — компонент).</summary>
    [DataField]
    public EntProtoId? Bake;

    [DataField]
    public float BakeTime = 30f;

    /// <summary>grillable для существующих блюд SS14.</summary>
    [DataField]
    public EntProtoId? Grill;

    [DataField]
    public float GrillTime = 30f;
}
