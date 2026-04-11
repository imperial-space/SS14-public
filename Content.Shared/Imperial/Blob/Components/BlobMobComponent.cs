namespace Content.Shared.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobMobComponent : Component
{
    [ViewVariables]
    public EntityUid? OwnerMind;

    [ViewVariables]
    public BlobChemicalType Chemical = BlobChemicalType.Toxin;
}