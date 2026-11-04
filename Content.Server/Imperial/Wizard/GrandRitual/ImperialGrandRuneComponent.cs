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

/// <summary>/obj/effect/grand_rune.</summary>
[RegisterComponent]
public sealed partial class ImperialGrandRuneComponent : Component
{
    /// <summary>potency: сколько ритуалов уже завершено.</summary>
    [ViewVariables]
    public int Potency;

    [ViewVariables]
    public TimeSpan InvokeTime;

    [ViewVariables]
    public bool InUse;

    [ViewVariables]
    public int TimesInvoked;

    [ViewVariables]
    public int CheeseSacrificed;

    [ViewVariables]
    public string[] MagicWords = Array.Empty<string>();

    /// <summary>Разум мага, чей это ритуал.</summary>
    [ViewVariables]
    public EntityUid Ritual;

    /// <summary>/obj/effect/grand_rune/finale.</summary>
    [DataField]
    public bool Finale;

    /// <summary>/obj/effect/grand_rune/finale/cheesy.</summary>
    [DataField]
    public bool Cheesy;

    [ViewVariables]
    public bool ChosenEffect;

    [ViewVariables]
    public string? FinaleEffect;

    [ViewVariables]
    public int DireWarningsGiven;

    [DataField]
    public string StatePrefix = "rune";
}
