using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.CombatStance;

[RegisterComponent]
public sealed partial class CombatStancePointComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Faction = "";
    public (bool up, bool bottom, bool right, bool left) PointDirection = (false, false, false, false);
    public (EntityUid? up, EntityUid? bottom, EntityUid? right, EntityUid? left) PointDirectionData = (null, null, null, null);
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FactionMemberGroup Group = FactionMemberGroup.None;
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<EntityUid> ValidMembers = new();
    public bool HasValidMember => ValidMembers.Count > 0;
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Participants = 0;
}
[Serializable, NetSerializable]
public enum CombatStanceAppearance : byte
{
    Key
}
