using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Server.Electrocution;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Server.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Item;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Temperature;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>Адаптация к холоду и/или жаре.</summary>
[RegisterComponent]
public sealed partial class GeneticAdaptationComponent : Component
{
    [DataField]
    public bool Cold;

    [DataField]
    public bool Heat;
}
