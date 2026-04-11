using Robust.Shared.ViewVariables;

namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobOvermindControllerComponent : Component
{
    [ViewVariables]
    public EntityUid? Overmind;
}