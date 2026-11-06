using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Rituals;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticFeastOwlsResultComponent : Component
{
    [DataField]
    public TimeSpan FeastCreatedTime = TimeSpan.FromSeconds(10);

    [DataField]
    public int FeastCreatedAmount = 5;
}
