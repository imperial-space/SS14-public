using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Mood;

/// <summary>/datum/quirk/item_quirk/family_heirloom.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkFamilyHeirloomComponent : Component
{
    [DataField]
    public List<EntProtoId> Heirlooms = new();

    [ViewVariables]
    public EntityUid? Item;
    [ViewVariables]
    public bool Given;
}
