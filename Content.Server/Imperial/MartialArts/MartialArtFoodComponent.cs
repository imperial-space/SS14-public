using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>obj/item/mushpunch: странный гриб, съешь — научишься.</summary>
[RegisterComponent]
public sealed partial class MartialArtFoodComponent : Component
{
    [DataField(required: true)]
    public ProtoId<MartialArtPrototype> Art;

    [DataField]
    public LocId Message = "martial-mushroom-devour";
}
