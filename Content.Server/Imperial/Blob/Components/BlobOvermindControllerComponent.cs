namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobOvermindControllerComponent : Component
{
    [DataField]
    public EntityUid? Overmind;
}