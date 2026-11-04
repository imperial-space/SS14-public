using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Eye;
using Content.Shared.Fluids.Components;
using Content.Shared.Fluids;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Throwing;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Demons;

/// <summary>/mob/living/basic/demon/slaughter (и laughter): данные демона.</summary>
[RegisterComponent]
public sealed partial class ImperialSlaughterDemonComponent : Component
{
    /// <summary>Демон смеха: не убивает навсегда, а «приглашает на вечеринку».</summary>
    [DataField]
    public bool Laughter;

    /// <summary>objective_verb.</summary>
    [DataField]
    public LocId ObjectiveVerb = "imperial-demon-verb-kill";

    [DataField]
    public LocId Greeting = "imperial-demon-greeting-slaughter";

    [DataField]
    public SoundSpecifier DeathSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/demon_dies.ogg");

    /// <summary>death_drops.</summary>
    [DataField]
    public List<EntProtoId> Loot = new();

    /// <summary>summoner: разум мага, разбившего бутылку.</summary>
    [ViewVariables]
    public EntityUid? Summoner;

    [ViewVariables]
    public bool ObjectivesGiven;

    /// <summary>consume_count.</summary>
    [ViewVariables]
    public int ConsumeCount;
}
