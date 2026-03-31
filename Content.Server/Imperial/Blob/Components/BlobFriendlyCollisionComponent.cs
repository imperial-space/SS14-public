using System.Collections.Generic;

namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobFriendlyCollisionComponent : Component
{
    [DataField]
    public Dictionary<string, int> DisabledFixtureMasks = new();
}