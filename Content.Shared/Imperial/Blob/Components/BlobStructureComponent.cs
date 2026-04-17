namespace Content.Shared.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobStructureComponent : Component
{
    [DataField]
    public EntityUid? OwnerMind;
}