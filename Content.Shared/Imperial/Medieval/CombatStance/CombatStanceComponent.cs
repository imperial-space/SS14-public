using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.CombatStance;

[RegisterComponent]
public sealed partial class CombatStanceComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool HasDefence = false;
}
