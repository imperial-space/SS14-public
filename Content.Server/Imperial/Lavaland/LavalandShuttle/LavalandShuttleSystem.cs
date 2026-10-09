using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Imperial.Lavaland.LavalandShuttle;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Station.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.LavalandShuttle;

/// <summary>
/// Консоль шаттла утилизаторов по образцу computer/shuttle из SS13: прогрев двигателей (Igniting),
/// перелёт (In Transit), таймер и статус. Срочная отправка быстрее, но с долгим откатом.
/// </summary>
public sealed class LavalandShuttleSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DockingSystem _dock = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    /// <summary>Тег PriorityDock у шлюзов ImperialAirlockShuttleLavaland.</summary>
    private const string DockTag = "ImperialDockLavaland";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandShuttleConsoleComponent, AfterActivatableUIOpenEvent>((uid, comp, _) => SendState(uid, comp));
        SubscribeLocalEvent<LavalandShuttleConsoleComponent, LavalandShuttleSetDestinationMessage>(OnSetDestination);
        SubscribeLocalEvent<LavalandShuttleConsoleComponent, LavalandShuttleDepartMessage>((uid, comp, msg) => Depart(uid, comp, msg.Actor, false));
        SubscribeLocalEvent<LavalandShuttleConsoleComponent, LavalandShuttleExpressDepartMessage>((uid, comp, msg) => Depart(uid, comp, msg.Actor, true));
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandShuttleConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUiUpdate)
                continue;
            comp.NextUiUpdate = now + TimeSpan.FromSeconds(1);
            if (_uiSystem.IsUiOpen(uid, LavalandShuttleConsoleUiKey.Key))
                SendState(uid, comp);
        }
    }

    private void OnSetDestination(EntityUid uid, LavalandShuttleConsoleComponent comp, LavalandShuttleSetDestinationMessage args)
    {
        if (!GetLocations(uid, comp).Contains(args.Destination))
            return;
        comp.SelectedDestination = args.Destination;
        SendState(uid, comp);
    }

    private bool TryGetShuttle(EntityUid console, out EntityUid grid, out ShuttleComponent shuttle)
    {
        grid = default;
        shuttle = default!;
        if (Transform(console).GridUid is not { } gridUid || !TryComp(gridUid, out ShuttleComponent? comp))
            return false;
        grid = gridUid;
        shuttle = comp;
        return true;
    }

    private EntityUid? GetStationGrid()
    {
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var stationUid, out _))
        {
            if (_station.GetLargestGrid(stationUid) is { } grid)
                return grid;
        }

        return null;
    }

    /// <summary>Где сейчас шаттл: на карте станции или лаваленда.</summary>
    private LavalandShuttleDestination CurrentLocation(EntityUid shuttleGrid, LavalandShuttleConsoleComponent comp)
    {
        var map = Transform(shuttleGrid).MapUid;
        if (comp.RecyclingOutpostGrid is { } outpost && Transform(outpost).MapUid == map)
            return LavalandShuttleDestination.Lavaland;
        if (GetStationGrid() is { } station && Transform(station).MapUid == map)
            return LavalandShuttleDestination.Station;
        return LavalandShuttleDestination.None;
    }

    /// <summary>get_valid_destinations: всё, кроме места, где шаттл уже пристыкован.</summary>
    private List<LavalandShuttleDestination> GetLocations(EntityUid console, LavalandShuttleConsoleComponent comp)
    {
        var result = new List<LavalandShuttleDestination>();
        if (!TryGetShuttle(console, out var grid, out _))
            return result;

        var current = CurrentLocation(grid, comp);
        if (current != LavalandShuttleDestination.Station && GetStationGrid() != null)
            result.Add(LavalandShuttleDestination.Station);
        if (current != LavalandShuttleDestination.Lavaland && comp.RecyclingOutpostGrid != null)
            result.Add(LavalandShuttleDestination.Lavaland);
        return result;
    }

    /// <summary>send_shuttle.</summary>
    private void Depart(EntityUid uid, LavalandShuttleConsoleComponent comp, EntityUid user, bool express)
    {
        if (!TryGetShuttle(uid, out var grid, out var shuttle))
            return;

        var locations = GetLocations(uid, comp);
        if (locations.Count == 1)
            comp.SelectedDestination = locations[0];

        string? error = null;
        if (HasComp<FTLComponent>(grid))
            error = "lavaland-shuttle-in-transit";
        else if (!_shuttle.CanFTL(grid, out _))
            error = "lavaland-shuttle-not-ready";
        else if (!locations.Contains(comp.SelectedDestination))
            error = "lavaland-shuttle-invalid-destination";
        else if (express && _timing.CurTime < comp.NextExpress)
            error = "lavaland-shuttle-express-recharging";

        if (error != null)
        {
            _popup.PopupEntity(Loc.GetString(error), uid, user);
            _audio.PlayPvs(comp.DenySound, uid);
            SendState(uid, comp);
            return;
        }

        var ignition = express ? comp.ExpressIgnitionTime : comp.IgnitionTime;
        var travel = express ? comp.ExpressTravelTime : comp.TravelTime;
        var sent = comp.SelectedDestination == LavalandShuttleDestination.Station
            ? DepartToStation(grid, shuttle, ignition, travel)
            : DepartToLavaland(grid, shuttle, comp, ignition, travel);

        if (!sent)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-shuttle-unable"), uid, user);
            return;
        }

        comp.TravelDestination = comp.SelectedDestination;
        if (express)
            comp.NextExpress = _timing.CurTime + comp.ExpressCooldown;

        _chat.TrySendInGameICMessage(uid, Loc.GetString(express ? "lavaland-shuttle-departing-express" : "lavaland-shuttle-departing"),
            InGameICChatType.Speak, ChatTransmitRange.Normal, hideLog: true, ignoreActionBlocker: true);
        SendState(uid, comp);
    }

    private bool DepartToStation(EntityUid grid, ShuttleComponent shuttle, float ignition, float travel)
    {
        if (GetStationGrid() is not { } stationGrid)
            return false;

        _shuttle.FTLToDock(grid, shuttle, stationGrid, startupTime: ignition, hyperspaceTime: travel, priorityTag: DockTag);
        return true;
    }

    private bool DepartToLavaland(EntityUid grid, ShuttleComponent shuttle, LavalandShuttleConsoleComponent comp, float ignition, float travel)
    {
        if (comp.RecyclingOutpostGrid is not { } outpost || Transform(outpost).MapUid is not { } lavalandMap)
            return false;

        // Стыковка к шлюзу аванпоста (ImperialAirlockShuttleLavaland); если стыковаться некуда — старая точка.
        if (_dock.GetDockingConfig(grid, outpost, DockTag) != null)
        {
            _shuttle.FTLToDock(grid, shuttle, outpost, startupTime: ignition, hyperspaceTime: travel, priorityTag: DockTag);
            return true;
        }

        var targetCoords = new EntityCoordinates(lavalandMap, new Vector2(-16f, -3f));
        _shuttle.FTLToCoordinates(grid, shuttle, targetCoords, Angle.Zero, startupTime: ignition, hyperspaceTime: travel);
        return true;
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        var shuttleUid = args.Entity;
        var consoleQuery = EntityQueryEnumerator<LavalandShuttleConsoleComponent, TransformComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out var console, out var xform))
        {
            if (xform.GridUid != shuttleUid)
                continue;
            console.TravelDestination = LavalandShuttleDestination.None;
            console.SelectedDestination = LavalandShuttleDestination.None;
            SendState(consoleUid, console);
        }
    }

    private string DestinationName(LavalandShuttleDestination destination)
    {
        return Loc.GetString(destination switch
        {
            LavalandShuttleDestination.Station => "lavaland-shuttle-location-station",
            LavalandShuttleDestination.Lavaland => "lavaland-shuttle-location-lavaland",
            _ => "lavaland-shuttle-location-unknown",
        });
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;
        return $"{(int) time.TotalMinutes:00}:{time.Seconds:00}";
    }

    /// <summary>ui_data.</summary>
    public void SendState(EntityUid uid, LavalandShuttleConsoleComponent comp)
    {
        if (!_uiSystem.HasUi(uid, LavalandShuttleConsoleUiKey.Key))
            return;

        var state = new LavalandShuttleConsoleBoundUserInterfaceState
        {
            ExpressCooldown = (int) Math.Ceiling(Math.Max(0, (comp.NextExpress - _timing.CurTime).TotalSeconds)),
        };

        if (!TryGetShuttle(uid, out var grid, out _))
        {
            state.Status = LavalandShuttleStatus.Missing;
            state.Location = Loc.GetString("lavaland-shuttle-location-unknown");
            state.Locked = true;
            _uiSystem.SetUiState(uid, LavalandShuttleConsoleUiKey.Key, state);
            return;
        }

        if (TryComp<FTLComponent>(grid, out var ftl) && ftl.State != FTLState.Available)
        {
            state.TimerStr = FormatTime(ftl.StateTime.End - _timing.CurTime);
            state.Status = ftl.State switch
            {
                FTLState.Starting => LavalandShuttleStatus.Igniting,
                FTLState.Cooldown => LavalandShuttleStatus.Recharging,
                _ => LavalandShuttleStatus.InTransit,
            };
            state.Location = ftl.State is FTLState.Travelling or FTLState.Arriving
                ? Loc.GetString("lavaland-shuttle-location-transit", ("destination", DestinationName(comp.TravelDestination)))
                : DestinationName(CurrentLocation(grid, comp));
            state.Locked = true;
            state.Destination = comp.TravelDestination;
        }
        else
        {
            state.Status = LavalandShuttleStatus.Idle;
            state.Location = DestinationName(CurrentLocation(grid, comp));
            state.Locations = GetLocations(uid, comp);
            if (state.Locations.Count == 1)
                comp.SelectedDestination = state.Locations[0];
            state.Destination = comp.SelectedDestination;
            if (state.Locations.Count == 0)
            {
                state.Locked = true;
                state.Status = LavalandShuttleStatus.Locked;
            }
        }

        _uiSystem.SetUiState(uid, LavalandShuttleConsoleUiKey.Key, state);
    }
}
