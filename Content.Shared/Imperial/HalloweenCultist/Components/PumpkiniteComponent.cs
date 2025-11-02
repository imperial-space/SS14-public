using Robust.Shared.Prototypes;
using Content.Shared.Imperial.HalloweenCultist;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.HalloweenCultist.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedHalloweenCultistSystem))]
public sealed partial class PumpkiniteComponent : Component
{
}
