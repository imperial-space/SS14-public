using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>/obj/machinery/netpod: капсула подключения к домену.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NetpodComponent : Component
{
    [DataField]
    public string ContainerId = "netpod-occupant";

    /// <summary>state_open.</summary>
    [DataField, AutoNetworkedField]
    public bool Open = true;

    /// <summary>BASE_DISCONNECT_DAMAGE: урон мозгу при насильном отключении.</summary>
    [DataField]
    public float DisconnectDamage = 40f;

    /// <summary>netsuit: снаряжение аватара, выбранное кликом по нетподу.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<Content.Shared.Roles.StartingGearPrototype> Outfit = "BitrunnerGear";

    /// <summary>netsuit: выбранная в окне профессия — её снаряжение и лоадаут по умолчанию. Пусто — Outfit.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<Content.Shared.Roles.JobPrototype>? Job;

    /// <summary>copy_body: аватар похож на битраннера.</summary>
    [DataField, AutoNetworkedField]
    public bool CopyBody;

    [ViewVariables]
    public EntityUid? Server;

    [ViewVariables]
    public EntityUid? Avatar;

    [ViewVariables]
    public bool Connected;

    [ViewVariables]
    public TimeSpan? AutoOpenAt;

    [ViewVariables]
    public TimeSpan NextHeal;
}
