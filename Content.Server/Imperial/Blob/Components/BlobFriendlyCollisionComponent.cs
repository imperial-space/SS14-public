using System.Collections.Generic;
using Robust.Shared.ViewVariables;

namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobFriendlyCollisionComponent : Component
{
    [ViewVariables]
    public Dictionary<string, int> DisabledFixtureMasks = new();
}