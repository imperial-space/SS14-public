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

/// <summary>/datum/action/cooldown/spell/jaunt/bloodcrawl (+ slaughter_demon / funny).</summary>
[RegisterComponent]
public sealed partial class ImperialBloodCrawlComponent : Component
{
    /// <summary>Пожирать тех, кого тащишь в кровь (slaughter_demon).</summary>
    [DataField]
    public bool Voracious;

    /// <summary>Вечеринка: съеденные хранятся и возвращаются после смерти демона.</summary>
    [DataField]
    public bool Funny;

    [DataField]
    public TimeSpan ExitTime = TimeSpan.FromSeconds(2);

    [DataField]
    public float Radius = 1.5f;

    [DataField]
    public SoundSpecifier ConsumeSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/demon_consume.ogg");

    [DataField]
    public EntProtoId Action = "ImperialActionBloodCrawl";

    [ViewVariables]
    public EntityUid? ActionEntity;

    /// <summary>resist_jaunt_damage: первый раз и во время еды кровь не разъедает плоть.</summary>
    [ViewVariables]
    public bool ResistDamage = true;

    [ViewVariables]
    public TimeSpan NextDamage;

    [ViewVariables]
    public EntityUid? Consuming;

    [ViewVariables]
    public int ConsumeStep;

    /// <summary>consumed_mobs.</summary>
    [ViewVariables]
    public List<EntityUid> Consumed = new();
}
