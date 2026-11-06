using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

[DataDefinition]
public sealed partial class CadiaDepotEntry
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public int Cost = 10;
}
