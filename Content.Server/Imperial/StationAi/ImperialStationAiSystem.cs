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
}

/// <summary>
/// Возможности ИИ из TG, которых нет в SS14: список камер, слежение, шлюз у цели, подсветка камер,
/// тревоги станции, оглашение законов и сенсоры. Действия выдаются мозгу, пока он в ядре (StationAiHeld).
/// </summary>
public sealed partial class ImperialStationAiSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedStationAiSystem _ai = default!;
    [Dependency] private readonly StationAiVisionSystem _vision = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedDoorSystem _doors = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SlimPoweredLightSystem _slimLight = default!;
    [Dependency] private readonly SiliconLawSystem _laws = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    private static readonly EntProtoId[] ActionProtos =
    {
        "ActionImperialAiCameraList",
        "ActionImperialAiTrack",
        "ActionImperialAiOpenDoor",
        "ActionImperialAiCameraLight",
        "ActionImperialAiAlerts",
        "ActionImperialAiStateLaws",
        "ActionImperialAiSensors",
        "ActionImperialAiAnnouncement",
        "ActionImperialAiStatusDisplay",
    };

    /// <summary>CAMERA_TICK_LIMIT.</summary>
    private const int TrackTickLimit = 10;
    private static readonly TimeSpan TrackInterval = TimeSpan.FromSeconds(0.2);
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LightInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan LawInterval = TimeSpan.FromSeconds(1);

    /// <summary>light_cameras(): камеры не дальше 7 клеток от «глаза».</summary>
    private const float CameraLightRange = 7f;

    /// <summary>open_nearest_door(): шлюзы в range(3, target).</summary>
    private const float DoorRange = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiHeldComponent, ComponentStartup>(OnHeldStartup);
        SubscribeLocalEvent<StationAiHeldComponent, ComponentShutdown>(OnHeldShutdown);

        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiCameraListActionEvent>(OnCameraList);
        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiTrackActionEvent>(OnTrack);
        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiOpenDoorActionEvent>(OnOpenDoor);
        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiCameraLightActionEvent>(OnCameraLight);
        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiAlertsActionEvent>(OnAlerts);
        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiStateLawsActionEvent>(OnStateLaws);
        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiSensorsActionEvent>(OnSensors);

        SubscribeNetworkEvent<ImperialAiListChoiceEvent>(OnListChoice);
        SubscribeNetworkEvent<ImperialAiJumpEvent>(OnJump);
        SubscribeNetworkEvent<ImperialAiStateLawsEvent>(OnStateLawsRequest);
        InitializeVox();
    }

    #region Действия

    private void OnHeldStartup(Entity<StationAiHeldComponent> ent, ref ComponentStartup args)
    {
        var comp = EnsureComp<ImperialStationAiComponent>(ent);
        if (comp.Actions.Count > 0)
            return;

        foreach (var proto in ActionProtos)
        {
            if (_actions.AddAction(ent, proto) is { } action)
                comp.Actions.Add(action);
        }
    }

    private void OnHeldShutdown(Entity<StationAiHeldComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<ImperialStationAiComponent>(ent, out var comp))
            return;

        foreach (var action in comp.Actions)
        {
            _actions.RemoveAction(ent.Owner, action);
        }

        SetCameraLights((ent, comp), false);
        SetSensors((ent, comp), false);
        RemComp<ImperialStationAiComponent>(ent);
    }

    private EntityUid? Eye(EntityUid ai)
    {
        return _ai.TryGetCore(ai, out var core) ? core.Comp?.RemoteEntity : null;
    }

    private EntityUid? CoreGrid(EntityUid ai)
    {
        return _ai.TryGetCore(ai, out var core) ? Transform(core.Owner).GridUid : null;
    }

    private void MoveEye(EntityUid ai, EntityCoordinates coords)
    {
        if (Eye(ai) is not { } eye)
            return;

        _transform.SetCoordinates(eye, coords);
        _transform.AttachToGridOrMap(eye);
    }

    #endregion

    #region Камеры и слежение

    /// <summary>show_camera_list(): работающие камеры станции ИИ.</summary>
    private void OnCameraList(Entity<ImperialStationAiComponent> ent, ref ImperialAiCameraListActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        var station = CoreStation(ent);
        var entries = new List<ImperialAiListEntry>();
        var query = EntityQueryEnumerator<SurveillanceCameraComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var camera, out var xform))
        {
            if (!camera.Active || xform.GridUid == null)
                continue;

            if (station != null && _station.GetOwningStation(uid, xform) != station)
                continue;

            var name = camera.NameSet || camera.CameraId != "camera" ? camera.CameraId : Name(uid);
            entries.Add(new ImperialAiListEntry(GetNetEntity(uid), name, _navMap.GetNearestBeaconString((uid, xform), true)));
        }

        RaiseNetworkEvent(new ImperialAiListEvent(ImperialAiListKind.Cameras,
            entries.OrderBy(e => e.Name).ToList()), actor.PlayerSession);
    }

    /// <summary>find_trackable_mobs(): гуманоиды по алфавиту, затем прочие — только видимые камерами.</summary>
    private void OnTrack(Entity<ImperialStationAiComponent> ent, ref ImperialAiTrackActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        var humans = new List<ImperialAiListEntry>();
        var others = new List<ImperialAiListEntry>();
        var names = new Dictionary<string, int>();
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (!CanTrack(ent, uid, xform))
                continue;

            var name = Name(uid);
            if (names.TryGetValue(name, out var count))
            {
                names[name] = count + 1;
                name = $"{name} ({count + 1})";
            }
            else
            {
                names[name] = 1;
            }

            var entry = new ImperialAiListEntry(GetNetEntity(uid), name, string.Empty);
            if (HasComp<HumanoidProfileComponent>(uid))
                humans.Add(entry);
            else
                others.Add(entry);
        }

        var list = humans.OrderBy(e => e.Name).Concat(others.OrderBy(e => e.Name)).ToList();
        RaiseNetworkEvent(new ImperialAiListEvent(ImperialAiListKind.Track, list), actor.PlayerSession);
    }

    /// <summary>can_track(): не сам ИИ, не в контейнере, на гриде и в поле зрения камер.</summary>
    private bool CanTrack(EntityUid ai, EntityUid target, TransformComponent? xform = null)
    {
        if (target == ai || !Resolve(target, ref xform) || xform.GridUid is not { } gridUid)
            return false;

        if (_containers.IsEntityInContainer(target) || HasComp<StationAiHeldComponent>(target))
            return false;

        if (!TryComp<BroadphaseComponent>(gridUid, out var broadphase) || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _maps.LocalToTile(gridUid, grid, xform.Coordinates);
        lock (_vision)
        {
            return _vision.IsAccessible((gridUid, broadphase, grid), tile);
        }
    }

    private void OnListChoice(ImperialAiListChoiceEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ai || !TryComp<ImperialStationAiComponent>(ai, out var comp)
            || !TryGetEntity(ev.Entity, out var target) || TerminatingOrDeleted(target))
            return;

        switch (ev.Kind)
        {
            case ImperialAiListKind.Cameras:
                if (!HasComp<SurveillanceCameraComponent>(target))
                    return;

                // switchCamera(): слежение сбрасывается.
                ResetTracking((ai, comp));
                MoveEye(ai, Transform(target.Value).Coordinates);
                break;
            case ImperialAiListKind.Track:
                if (!CanTrack(ai, target.Value))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-ai-track-failed"), ai, ai);
                    return;
                }

                comp.Tracked = target;
                comp.TrackFails = 0;
                comp.TrackPlaced = null;
                _popup.PopupEntity(Loc.GetString("imperial-ai-track-start", ("target", Name(target.Value))), ai, ai);
                break;
        }
    }

    private void ResetTracking(Entity<ImperialStationAiComponent> ent)
    {
        ent.Comp.Tracked = null;
        ent.Comp.TrackFails = 0;
        ent.Comp.TrackPlaced = null;
    }

    /// <summary>target_moved() / process(): «глаз» идёт за целью, пропавшую цель ищет 10 раз.</summary>
    private void UpdateTracking(Entity<ImperialStationAiComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Tracked is not { } target || now < ent.Comp.NextTrackCheck || Eye(ent) is not { } eye)
            return;

        // Ручное движение «глазом» снимает слежение (reset_tracking в relaymove).
        if (ent.Comp.TrackPlaced is { } placed
            && !_transform.InRange(Transform(eye).Coordinates, placed, 0.75f))
        {
            ResetTracking(ent);
            return;
        }

        if (TerminatingOrDeleted(target))
        {
            ResetTracking(ent);
            return;
        }

        if (CanTrack(ent, target))
        {
            ent.Comp.TrackFails = 0;
            ent.Comp.NextTrackCheck = now + TrackInterval;
            var coords = Transform(target).Coordinates;
            MoveEye(ent, coords);
            ent.Comp.TrackPlaced = Transform(eye).Coordinates;
            return;
        }

        ent.Comp.NextTrackCheck = now + RecheckInterval;
        if (ent.Comp.TrackFails == 0)
            _popup.PopupEntity(Loc.GetString("imperial-ai-track-lost"), ent, ent, PopupType.SmallCaution);

        if (++ent.Comp.TrackFails < TrackTickLimit)
            return;

        _popup.PopupEntity(Loc.GetString("imperial-ai-track-cancel"), ent, ent, PopupType.MediumCaution);
        ResetTracking(ent);
    }

    /// <summary>open_nearest_door(): ближайший закрытый шлюз в 3 клетках от цели.</summary>
    private void OnOpenDoor(Entity<ImperialStationAiComponent> ent, ref ImperialAiOpenDoorActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Tracked is not { } target || TerminatingOrDeleted(target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-door-no-target"), ent, ent);
            return;
        }

        if (!CanTrack(ent, target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-door-not-visible"), ent, ent);
            return;
        }

        var coords = Transform(target).Coordinates;
        Entity<DoorComponent>? best = null;
        var bestDist = float.MaxValue;
        foreach (var door in _lookup.GetEntitiesInRange<DoorComponent>(coords, DoorRange))
        {
            if (door.Comp.State != DoorState.Closed || !HasComp<AirlockComponent>(door))
                continue;

            if (!_transform.InRange(Transform(door).Coordinates, coords, DoorRange))
                continue;

            var dist = (_transform.GetWorldPosition(door) - _transform.GetWorldPosition(target)).Length();
            if (dist < bestDist)
            {
                best = door;
                bestDist = dist;
            }
        }

        if (best is not { } found)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-door-none"), ent, ent);
            return;
        }

        var name = Name(found);
        if (TryComp<AirlockComponent>(found, out var airlock) && !airlock.Powered)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-door-unpowered", ("door", name)), ent, ent);
            return;
        }

        if (TryComp<DoorBoltComponent>(found, out var bolts) && bolts.BoltsDown)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-door-bolted", ("door", name)), ent, ent);
            return;
        }

        if (TryComp<WeldableComponent>(found, out var weld) && weld.IsWelded)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-door-welded", ("door", name)), ent, ent);
            return;
        }

        if (TryComp<StationAiWhitelistComponent>(found, out var whitelist) && !whitelist.Enabled)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-door-no-response", ("door", name)), ent, ent);
            return;
        }

        _doors.StartOpening(found, found.Comp, ent);
        _popup.PopupEntity(Loc.GetString("imperial-ai-door-opened", ("door", name), ("target", Name(target))), ent, ent);
    }

    #endregion

    #region Подсветка камер

    private void OnCameraLight(Entity<ImperialStationAiComponent> ent, ref ImperialAiCameraLightActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetCameraLights(ent, !ent.Comp.CameraLights);
        _popup.PopupEntity(Loc.GetString(ent.Comp.CameraLights ? "imperial-ai-lights-on" : "imperial-ai-lights-off"), ent, ent);
    }

    private void SetCameraLights(Entity<ImperialStationAiComponent> ent, bool enabled)
    {
        ent.Comp.CameraLights = enabled;
        if (enabled)
            return;

        foreach (var camera in ent.Comp.LitCameras)
        {
            if (!TerminatingOrDeleted(camera))
                _slimLight.SetEnabled(camera, false);
        }

        ent.Comp.LitCameras.Clear();
    }

    /// <summary>light_cameras(): включить свет у камер рядом с «глазом», погасить у остальных.</summary>
    private void UpdateCameraLights(Entity<ImperialStationAiComponent> ent, TimeSpan now)
    {
        if (!ent.Comp.CameraLights || now < ent.Comp.NextLightUpdate || Eye(ent) is not { } eye)
            return;

        ent.Comp.NextLightUpdate = now + LightInterval;
        var visible = new HashSet<EntityUid>();
        foreach (var camera in _lookup.GetEntitiesInRange<SurveillanceCameraComponent>(Transform(eye).Coordinates, CameraLightRange))
        {
            if (camera.Comp.Active && HasComp<SlimPoweredLightComponent>(camera))
                visible.Add(camera);
        }

        foreach (var camera in ent.Comp.LitCameras.Except(visible).ToList())
        {
            ent.Comp.LitCameras.Remove(camera);
            if (!TerminatingOrDeleted(camera))
                _slimLight.SetEnabled(camera, false);
        }

        foreach (var camera in visible.Except(ent.Comp.LitCameras).ToList())
        {
            ent.Comp.LitCameras.Add(camera);
            _slimLight.SetEnabled(camera, true);
        }
    }

    #endregion

    #region Тревоги

    private EntityUid? CoreStation(EntityUid ai)
    {
        return _ai.TryGetCore(ai, out var core) ? _station.GetOwningStation(core.Owner) : null;
    }

    /// <summary>Тревоги станции: пожар, атмосфера, питание, камеры — как alarm_types у ИИ.</summary>
    private void OnAlerts(Entity<ImperialStationAiComponent> ent, ref ImperialAiAlertsActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        var station = CoreStation(ent);
        var alarms = new List<ImperialAiAlarmEntry>();

        void Add(ImperialAiAlarmKind kind, EntityUid source, TransformComponent xform)
        {
            if (station != null && _station.GetOwningStation(source, xform) != station)
                return;

            alarms.Add(new ImperialAiAlarmEntry(kind, GetNetEntity(source), _navMap.GetNearestBeaconString((source, xform), true)));
        }

        var alarmQuery = EntityQueryEnumerator<AtmosAlarmableComponent, TransformComponent>();
        while (alarmQuery.MoveNext(out var uid, out var alarmable, out var xform))
        {
            if (alarmable.LastAlarmState != AtmosAlarmType.Danger)
                continue;

            if (HasComp<FireAlarmComponent>(uid))
                Add(ImperialAiAlarmKind.Fire, uid, xform);
            else if (HasComp<AirAlarmComponent>(uid))
                Add(ImperialAiAlarmKind.Atmosphere, uid, xform);
        }

        var apcQuery = EntityQueryEnumerator<ApcComponent, TransformComponent>();
        while (apcQuery.MoveNext(out var uid, out var apc, out var xform))
        {
            if (apc.LastExternalState == ApcExternalPowerState.None || !apc.MainBreakerEnabled)
                Add(ImperialAiAlarmKind.Power, uid, xform);
        }

        var cameraQuery = EntityQueryEnumerator<SurveillanceCameraComponent, TransformComponent>();
        while (cameraQuery.MoveNext(out var uid, out var camera, out var xform))
        {
            if (!camera.Active && xform.Anchored)
                Add(ImperialAiAlarmKind.Camera, uid, xform);
        }

        RaiseNetworkEvent(new ImperialAiAlertsEvent(alarms), actor.PlayerSession);
    }

    private void OnJump(ImperialAiJumpEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ai || !TryComp<ImperialStationAiComponent>(ai, out var comp)
            || !TryGetEntity(ev.Target, out var target) || TerminatingOrDeleted(target))
            return;

        if (!HasComp<AtmosAlarmableComponent>(target) && !HasComp<ApcComponent>(target)
            && !HasComp<SurveillanceCameraComponent>(target))
            return;

        ResetTracking((ai, comp));
        MoveEye(ai, Transform(target.Value).Coordinates);
    }

    #endregion

    #region Законы

    /// <summary>checklaws(): окно с законами и выбором канала.</summary>
    private void OnStateLaws(Entity<ImperialStationAiComponent> ent, ref ImperialAiStateLawsActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        var laws = _laws.GetLaws(ent).Laws
            .Select(l => $"{l.LawIdentifierOverride ?? l.Order.ToString()}. {Loc.GetString(l.LawString)}")
            .ToList();

        var channels = new List<ImperialAiChannelEntry>();
        if (TryComp<IntrinsicRadioTransmitterComponent>(ent, out var transmitter))
        {
            foreach (var id in transmitter.Channels)
            {
                if (_proto.TryIndex(id, out var channel))
                    channels.Add(new ImperialAiChannelEntry(channel.ID, channel.LocalizedName));
            }
        }

        RaiseNetworkEvent(new ImperialAiLawsEvent(laws, channels), actor.PlayerSession);
    }

    private void OnStateLawsRequest(ImperialAiStateLawsEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ai || !TryComp<ImperialStationAiComponent>(ai, out var comp))
            return;

        string? prefix = null;
        if (ev.Channel != null)
        {
            if (!TryComp<IntrinsicRadioTransmitterComponent>(ai, out var transmitter)
                || !transmitter.Channels.Contains(ev.Channel)
                || !_proto.TryIndex<RadioChannelPrototype>(ev.Channel, out var channel))
                return;

            prefix = $"{SharedChatSystem.RadioChannelPrefix}{channel.KeyCode} ";
        }

        var laws = _laws.GetLaws(ai).Laws;
        comp.PendingLaws.Clear();
        comp.LawChannel = prefix;
        comp.PendingLaws.Enqueue(Loc.GetString("imperial-ai-laws-header"));
        foreach (var index in ev.Laws.Distinct().Order())
        {
            if (index < 0 || index >= laws.Count)
                continue;

            var law = laws[index];
            comp.PendingLaws.Enqueue($"{law.LawIdentifierOverride ?? law.Order.ToString()}. {Loc.GetString(law.LawString)}");
        }

        comp.NextLaw = _timing.CurTime;
    }

    /// <summary>statelaws(): закон за законом с паузой в секунду.</summary>
    private void UpdateLaws(Entity<ImperialStationAiComponent> ent, TimeSpan now)
    {
        if (ent.Comp.PendingLaws.Count == 0 || now < ent.Comp.NextLaw)
            return;

        ent.Comp.NextLaw = now + LawInterval;
        var line = ent.Comp.PendingLaws.Dequeue();
        _chat.TrySendInGameICMessage(ent, (ent.Comp.LawChannel ?? string.Empty) + line, InGameICChatType.Speak,
            hideChat: false, checkRadioPrefix: true, ignoreActionBlocker: true);
    }

    #endregion

    #region Сенсоры

    private void OnSensors(Entity<ImperialStationAiComponent> ent, ref ImperialAiSensorsActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetSensors(ent, !ent.Comp.Sensors);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Sensors ? "imperial-ai-sensors-on" : "imperial-ai-sensors-off"), ent, ent);
    }

    /// <summary>toggle_sensors(): медицинский (здоровье) и охранный (розыск, имплант защиты) HUD.</summary>
    private void SetSensors(Entity<ImperialStationAiComponent> ent, bool enabled)
    {
        ent.Comp.Sensors = enabled;
        if (enabled)
        {
            EnsureComp<ShowHealthBarsComponent>(ent);
            EnsureComp<ShowHealthIconsComponent>(ent);
            EnsureComp<ShowCriminalRecordIconsComponent>(ent);
            EnsureComp<ShowMindShieldIconsComponent>(ent);
            return;
        }

        RemComp<ShowHealthBarsComponent>(ent);
        RemComp<ShowHealthIconsComponent>(ent);
        RemComp<ShowCriminalRecordIconsComponent>(ent);
        RemComp<ShowMindShieldIconsComponent>(ent);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialStationAiComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);
            UpdateTracking(ent, now);
            UpdateCameraLights(ent, now);
            UpdateLaws(ent, now);
        }
    }
}
