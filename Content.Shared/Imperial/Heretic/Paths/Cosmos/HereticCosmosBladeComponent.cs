using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Cosmos;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticCosmosBladeComponent : Component
{
    [DataField]
    public TimeSpan CosmosBladeMeleeHitTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan CosmosBladeMeleeHitDuration = TimeSpan.FromSeconds(30);
}
