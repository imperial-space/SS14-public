using Content.Shared.Imperial.Blob;

namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobRuleSourceComponent : Component
{
    [ViewVariables]
    public EntityUid? Rule;

    [ViewVariables]
    public BlobChemicalType Chemical = BlobChemicalType.Sorium;
}