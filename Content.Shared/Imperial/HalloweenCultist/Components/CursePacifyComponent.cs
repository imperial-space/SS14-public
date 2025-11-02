using Robust.Shared.Prototypes;
using Content.Shared.Imperial.HalloweenCultist;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.HalloweenCultist.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CursePacifyComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Time = TimeSpan.FromSeconds(5f);
}