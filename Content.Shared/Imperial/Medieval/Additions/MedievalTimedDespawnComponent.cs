using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Medieval.Additions;

[RegisterComponent]
public sealed partial class MedievalTimedDespawnComponent : Component
{
    [DataField("lifetime")]
    public float Lifetime = 5f;
    public float OriginalLifeTime = 5f;
}
