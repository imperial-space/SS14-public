using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>Диск битраннинга: выбор делается один раз, загрузка — при входе в нетпод с диском при себе.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BitrunningDiskComponent : Component
{
    [DataField(required: true)]
    public List<BitrunningDiskOption> Options = new();

    [DataField, AutoNetworkedField]
    public int? Choice;
}
