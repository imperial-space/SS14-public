using System.Linq;
using Content.Server.Imperial.Wizard;
using Content.Server.Objectives.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Roles.Jobs;
using Content.Shared.Traits;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.GrandRitual;

/// <summary>Тело, ждущее воскрешения при «Увековечивании» (/obj/effect/spectre_of_resurrection).</summary>
[RegisterComponent]
public sealed partial class ImperialSpectreOfResurrectionComponent : Component
{
    [ViewVariables]
    public EntityUid? Corpse;

    [ViewVariables]
    public TimeSpan ReviveAt;
}
