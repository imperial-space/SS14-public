using System.Linq;
using Content.Server.Administration;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.Wizard.GrandRitual;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Shared.Administration;
using Content.Shared.Dataset;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Robust.Shared.Console;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

[RegisterComponent]
public sealed partial class ImperialWizardRuleComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Wizards = new();
}
