using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticBladeBladeComponent : Component
{
    [DataField]
    public bool Infused = false;
}
