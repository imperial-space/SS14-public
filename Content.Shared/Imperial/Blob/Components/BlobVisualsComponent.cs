using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob.Components;

/// <summary>Как клиент окрашивает спрайт по <see cref="BlobVisuals"/>.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BlobVisualsComponent : Component
{
    [DataField]
    public BlobVisualsMode Mode = BlobVisualsMode.Structure;
}
