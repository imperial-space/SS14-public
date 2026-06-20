using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.Imperial.Lavaland.MegafaunaSleep;

[RegisterComponent]
public sealed partial class LavalandMegafaunaSleepComponent : Component
{
    [DataField]
    public float WakeRadius = 7f;
}
