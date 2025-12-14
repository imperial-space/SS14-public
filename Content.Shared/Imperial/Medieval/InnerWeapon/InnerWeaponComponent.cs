using Content.Shared.Polymorph;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class InnerWeaponComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Current = "";
}
