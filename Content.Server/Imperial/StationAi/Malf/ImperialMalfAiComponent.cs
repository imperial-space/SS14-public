using System.Linq;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.Power.Components;
using Content.Server.Silicons.Laws;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.StationAi;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Map;

namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>/datum/antagonist/malf_ai + malf_picker: состояние сбойного ИИ на его мозге.</summary>
[RegisterComponent]
public sealed partial class ImperialMalfAiComponent : Component
{
    /// <summary>processing_time.</summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int ProcessingTime = 50;

    /// <summary>hacked_apcs.</summary>
    [ViewVariables]
    public HashSet<EntityUid> HackedApcs = new();

    /// <summary>employer: вариант сбоя, от него закон 0 и вступление.</summary>
    [ViewVariables]
    public string Employer = string.Empty;

    /// <summary>malfhack: ЛКП, который сейчас взламывается.</summary>
    [ViewVariables]
    public EntityUid? Hacking;

    [ViewVariables]
    public TimeSpan HackEnd;

    /// <summary>Купленные модули (для one_purchase).</summary>
    [ViewVariables]
    public HashSet<string> Bought = new();

    /// <summary>Действие модуля и оставшиеся применения.</summary>
    [ViewVariables]
    public Dictionary<string, (EntityUid Action, int Uses)> Modules = new();

    [ViewVariables]
    public List<EntityUid> Actions = new();

    [ViewVariables]
    public TimeSpan NextCheck;

    /// <summary>can_shunt: фабрика и Судный день запрещают перенос.</summary>
    [ViewVariables]
    public bool CanShunt = true;

    [ViewVariables]
    public bool XrayCameras;

    [ViewVariables]
    public bool HeavyTurrets;

    [ViewVariables]
    public bool Eavesdrop;

    [ViewVariables]
    public string? VoiceName;

    /// <summary>Взломанные ЛКП командных отсеков (скидка на Судный день).</summary>
    [ViewVariables]
    public HashSet<EntityUid> CommandApcs = new();

    [ViewVariables]
    public TimeSpan? DoomsdayEnd;

    [ViewVariables]
    public TimeSpan NextDoomsdayAnnounce;

    [ViewVariables]
    public EntityUid? DoomsdayAction;

    /// <summary>Ядро-укрытие в ЛКП, если ИИ перенёсся.</summary>
    [ViewVariables]
    public EntityUid? Shunt;

    [ViewVariables]
    public EntityUid? ShuntAction;

    [ViewVariables]
    public EntityUid? ReturnAction;
}
