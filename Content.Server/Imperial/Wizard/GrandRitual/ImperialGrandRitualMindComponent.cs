using System.Linq;
using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Station.Systems;
using Content.Server.StationEvents;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.GrandRitual;

/// <summary>Состояние Великого ритуала мага (/datum/action/cooldown/grand_ritual) — живёт на разуме.</summary>
[RegisterComponent]
public sealed partial class ImperialGrandRitualMindComponent : Component
{
    /// <summary>times_completed.</summary>
    [ViewVariables]
    public int TimesCompleted;

    /// <summary>target_area: маяк узла силы.</summary>
    [ViewVariables]
    public EntityUid? TargetBeacon;

    [ViewVariables]
    public string TargetName = string.Empty;

    /// <summary>drew_finale.</summary>
    [ViewVariables]
    public bool DrewFinale;

    /// <summary>drawing_rune.</summary>
    [ViewVariables]
    public bool Drawing;

    /// <summary>total_cheese_sacrificed.</summary>
    [ViewVariables]
    public int CheeseSacrificed;

    [ViewVariables]
    public bool CheeseGoalMet;

    /// <summary>rune: руна, нарисованная для текущего узла.</summary>
    [ViewVariables]
    public EntityUid? Rune;

    [ViewVariables]
    public EntityUid? Action;
}
