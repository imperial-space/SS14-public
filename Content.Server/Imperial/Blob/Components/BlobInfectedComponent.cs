namespace Content.Server.Imperial.Blob.Components;

using Content.Shared.Imperial.Blob;

[RegisterComponent]
public sealed partial class BlobInfectedComponent : Component
{
    [DataField]
    public float TransformDelay = 8f;

    [ViewVariables]
    public float TransformAccumulator;

    [ViewVariables]
    public EntityUid? OwnerMind;

    [ViewVariables]
    public BlobChemicalType Chemical = BlobChemicalType.Toxin;
}