using Content.Shared.Imperial.Blob;

namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobChemicalEffectComponent : Component
{
    [DataField]
    public BlobChemicalType Chemical = BlobChemicalType.Toxin;

    [DataField]
    public float TickInterval = 1f;

    [ViewVariables]
    public float TickAccumulator;

    [ViewVariables]
    public float TimeRemaining;
}