using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Flesh;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticRawProphetComponent : Component
{
    [DataField]
    public EntityUid? Master;

    [DataField]
    public TimeSpan BlindTime = TimeSpan.FromSeconds(8);
}
