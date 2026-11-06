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

/// <summary>/datum/grand_finale: описание варианта гранд-финала.</summary>
public sealed record ImperialGrandFinale(string Id, TimeSpan MinimumTime, TimeSpan InvokeTime, bool DireWarning);
