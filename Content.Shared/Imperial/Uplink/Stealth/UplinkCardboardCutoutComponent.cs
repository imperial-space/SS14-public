using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Картонная фигура: смена облика мелком.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UplinkCardboardCutoutComponent : Component
{
    [DataField]
    public List<string> States = new();

    [DataField, AutoNetworkedField]
    public int Index;
}
