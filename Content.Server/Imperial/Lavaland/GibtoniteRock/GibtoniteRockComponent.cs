using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.Imperial.Lavaland.GibtoniteRock;

[RegisterComponent]
public sealed partial class GibtoniteRockComponent : Component
{
    [DataField]
    public float ActivationTime = 5.0f;

    public bool IsActive = false;
    public bool IsDefused = false;
    public float Timer = 0f;
}
