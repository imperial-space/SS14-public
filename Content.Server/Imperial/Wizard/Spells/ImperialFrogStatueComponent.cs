using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Station;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>/obj/item/frog_statue: выпустить или отозвать лягушку (раз в 30 секунд).</summary>
[RegisterComponent]
public sealed partial class ImperialFrogStatueComponent : Component
{
    [ViewVariables]
    public EntityUid? Frog;

    [ViewVariables]
    public TimeSpan NextUse;

    [DataField]
    public string Container = "imperial_frog";
}
