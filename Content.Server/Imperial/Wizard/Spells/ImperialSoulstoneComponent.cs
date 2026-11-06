using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>/obj/item/soulstone/mystic: камень душ мага — пленит душу павшего в тень.</summary>
[RegisterComponent]
public sealed partial class ImperialSoulstoneComponent : Component
{
    [DataField]
    public string Container = "imperial_soulstone";

    /// <summary>required_role: камнем пользуются только маги и их прислужники.</summary>
    [DataField]
    public bool WizardOnly = true;

    [DataField]
    public EntProtoId Shade = "ImperialWizardShade";

    [DataField]
    public TimeSpan CanUseDuration = TimeSpan.FromSeconds(10);
}
