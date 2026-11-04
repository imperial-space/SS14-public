using System.Numerics;
using Content.Shared.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Uplink.AssaultPod;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Uplink.AssaultPod;

/// <summary>Консоль большого штурмового пода «Steel Rain» (computer/shuttle/syndicate/drop_pod).</summary>
[RegisterComponent]
public sealed partial class AssaultPodConsoleComponent : Component
{
    [DataField]
    public TimeSpan Countdown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan FlightTime = TimeSpan.FromSeconds(20);

    /// <summary>false — экипаж выбирает только зону, а старт даёт сценарий (протокол «Эпсилон»).</summary>
    [DataField]
    public bool ManualLaunch = true;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public string? TargetName;

    [ViewVariables]
    public AssaultPodState State = AssaultPodState.Ready;

    [ViewVariables]
    public TimeSpan NextStage;
}
