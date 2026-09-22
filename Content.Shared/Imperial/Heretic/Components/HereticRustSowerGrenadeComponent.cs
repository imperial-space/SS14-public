using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticRustSowerGrenadeComponent : Component
{
    [DataField]
    public float Range = 3f;

    [DataField]
    public TimeSpan FlashDuration = TimeSpan.FromSeconds(10);

    [DataField]
    public float SiliconDamage = 500f;
}
