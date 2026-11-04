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

/// <summary>Данные мага на разуме: перки, покупки для итогов раунда, учитель ученика.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardMindComponent : Component
{
    /// <summary>perks.</summary>
    [ViewVariables]
    public HashSet<string> Perks = new();

    /// <summary>GLOB.wizard_spellbook_purchases_by_key: запись → сколько раз куплена.</summary>
    [ViewVariables]
    public Dictionary<string, int> Purchases = new();

    [ViewVariables]
    public bool Apprentice;

    [ViewVariables]
    public EntityUid? Master;

    /// <summary>Выживальщик после «Призыва оружия/магии» — не маг и не прислужник.</summary>
    [ViewVariables]
    public bool Survivalist;
}
