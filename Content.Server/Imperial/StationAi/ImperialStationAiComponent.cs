using System.Linq;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Chat.Systems;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Silicons.Laws;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.APC;
using Content.Shared.Atmos.Monitor;
using Content.Shared.Chat;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.StationAi;

/// <summary>Состояние доработок ИИ на мозге в ядре.</summary>
[RegisterComponent]
public sealed partial class ImperialStationAiComponent : Component
{
    /// <summary>last_announcement.</summary>
    [ViewVariables]
    public string LastAnnouncement = string.Empty;

    [ViewVariables]
    public List<EntityUid> Actions = new();

    /// <summary>ai_tracking_tool.tracked_mob.</summary>
    [ViewVariables]
    public EntityUid? Tracked;

    /// <summary>cameraticks: сколько раз цель не нашлась на камерах.</summary>
    [ViewVariables]
    public int TrackFails;

    /// <summary>Куда «глаз» поставлен слежением — сдвиг оттуда значит, что ИИ двигается сам.</summary>
    [ViewVariables]
    public EntityCoordinates? TrackPlaced;

    [ViewVariables]
    public TimeSpan NextTrackCheck;

    /// <summary>camera_light_on.</summary>
    [ViewVariables]
    public bool CameraLights;

    /// <summary>lit_cameras.</summary>
    [ViewVariables]
    public HashSet<EntityUid> LitCameras = new();

    [ViewVariables]
    public TimeSpan NextLightUpdate;

    /// <summary>sensors_on: медицинский и охранный HUD.</summary>
    [ViewVariables]
    public bool Sensors;

    /// <summary>Очередь оглашения законов: по строке в секунду.</summary>
    [ViewVariables]
    public Queue<string> PendingLaws = new();

    [ViewVariables]
    public string? LawChannel;

    [ViewVariables]
    public TimeSpan NextLaw;

    [DataField]
    public float UpdateTrackingRange = 0.75f;
}
