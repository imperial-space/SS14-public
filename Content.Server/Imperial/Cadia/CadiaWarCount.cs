using System.Linq;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Итоги войны: кто чем отличился, плюс медали, которые офицеры вручают прямо в бою.</summary>
/// <summary>События войны, которые попадают в итоги числом.</summary>
public enum CadiaWarCount : byte
{
    PodsLanded,
    PodsShotDown,
    RefugeesSaved,
    RefugeesDied,
    RitualsChaos,
    RitualsImperium,
    Boardings,
    BerthsWrecked,
}
