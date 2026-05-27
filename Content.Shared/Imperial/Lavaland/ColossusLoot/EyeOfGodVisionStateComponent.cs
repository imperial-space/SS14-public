using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Lavaland.ColossusLoot;

[RegisterComponent]
public sealed partial class EyeOfGodVisionStateComponent : Component
{
    [ViewVariables]
    public bool Applied;

    [ViewVariables]
    public bool PreviousDrawFov = true;

    [ViewVariables]
    public bool PreviousDrawLight = true;
}
