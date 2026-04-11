using Content.Shared.Imperial.Blob;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobMouseComponent : Component
{
    [DataField]
    public EntProtoId TransformActionPrototype = "ActionBlobMouseTransform";

    [DataField]
    public float TransformDelay = 120f;

    [ViewVariables]
    public float TransformAccumulator;

    [ViewVariables]
    public EntityUid? TransformAction;

    [ViewVariables]
    public BlobChemicalType Chemical = BlobChemicalType.Sorium;

    [ViewVariables]
    public EntityUid? SourceRule;

    [ViewVariables]
    public bool Triggered;
}