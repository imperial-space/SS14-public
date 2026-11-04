using System.Numerics;
using Content.Server.AlertLevel;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.Uplink.AssaultPod;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Epsilon;
using Content.Shared.Light.Components;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Content.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Epsilon;

/// <summary>
/// Протокол «Эпсилон»: под эскадрона смерти ждёт у ЦК, экипаж пода выбирает зону высадки,
/// а сценарий сам запускает оповещение, гасит станцию и сажает под ровно на 1:28 трека.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialEpsilonRuleComponent : Component
{
    /// <summary>Через сколько после появления пода оповещение начнётся само (команда «epsilon start» — сразу).</summary>
    [DataField]
    public TimeSpan PrepTime = TimeSpan.FromMinutes(5);

    [DataField]
    public int Operatives = 8;

    [DataField]
    public EntProtoId Operative = "ImperialEpsilonOperative";

    /// <summary>Во сколько раз тусклее светят лампы после смены кода.</summary>
    [DataField]
    public float LightDim = 0.35f;

    [DataField]
    public string AlertLevel = "epsilon";

    [ViewVariables]
    public TimeSpan AutoStartAt;

    [ViewVariables]
    public TimeSpan? SequenceStart;

    [ViewVariables]
    public EntityUid? Pod;

    [ViewVariables]
    public EntityUid? Console;

    [ViewVariables]
    public EpsilonStage Stage = EpsilonStage.Waiting;

    [ViewVariables]
    public List<EntityUid> Lights = new();

    [ViewVariables]
    public int LightsOff;

    [ViewVariables]
    public TimeSpan NextEmergencyRefresh;
}
