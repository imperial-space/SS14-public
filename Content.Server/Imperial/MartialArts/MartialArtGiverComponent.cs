using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>component/martial_art_giver: одежда, которая учит искусству, пока надета.</summary>
[RegisterComponent]
public sealed partial class MartialArtGiverComponent : Component
{
    [DataField(required: true)]
    public ProtoId<MartialArtPrototype> Art;

    [ViewVariables]
    public EntityUid? Wearer;
}
