using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalFactionMemberComponent : Component
{

    [DataField]
    [AutoNetworkedField]
    public ProtoId<MedievalFactionPrototype> Faction { get; set; } = string.Empty;

    [AutoNetworkedField]
    public EntityUid? FactionMenuActionEntity;

    [DataField]
    public EntProtoId FactionMenuAction = "BaseFactionMenuAction";

    [DataField]
    public int Priority = 1;

    [DataField]
    [AutoNetworkedField]
    public FactionMenuAccess MenuAccess = FactionMenuAccess.None;

    [AutoNetworkedField]
    public int MemberID = 0;

    [AutoNetworkedField]
    public KeyValuePair<ProtoId<MedievalFactionPrototype>, string>? Wanted;
}

[Serializable, NetSerializable]
public enum FactionMenuAccess : byte
{
    None,
    Group,
    Full
}
