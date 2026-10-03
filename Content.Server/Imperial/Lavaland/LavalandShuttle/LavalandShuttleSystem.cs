using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Imperial.Lavaland.LavalandShuttle;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.LavalandShuttle;

public sealed class LavalandShuttleSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandShuttleConsoleComponent, AfterActivatableUIOpenEvent>(OnUIOpened);
        SubscribeLocalEvent<LavalandShuttleConsoleComponent, LavalandShuttleFlyToStationMessage>(OnSelectStation);
        SubscribeLocalEvent<LavalandShuttleConsoleComponent, LavalandShuttleFlyToLavalandMessage>(OnSelectLavaland);
        SubscribeLocalEvent<LavalandShuttleConsoleComponent, LavalandShuttleDepartMessage>(OnDepart);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
    }

    private void OnUIOpened(EntityUid uid, LavalandShuttleConsoleComponent comp, AfterActivatableUIOpenEvent args)
    {
        SendState(uid, comp);
    }

    private void OnSelectStation(EntityUid uid, LavalandShuttleConsoleComponent comp, LavalandShuttleFlyToStationMessage args)
    {
        comp.SelectedDestination = LavalandShuttleDestination.Station;
        SendState(uid, comp);
    }

    private void OnSelectLavaland(EntityUid uid, LavalandShuttleConsoleComponent comp, LavalandShuttleFlyToLavalandMessage args)
    {
        comp.SelectedDestination = LavalandShuttleDestination.Lavaland;
        SendState(uid, comp);
    }

    private void OnDepart(EntityUid uid, LavalandShuttleConsoleComponent comp, LavalandShuttleDepartMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
            return;

        if (!TryComp(xform.GridUid.Value, out ShuttleComponent? shuttleComp))
            return;

        if (!_shuttle.CanFTL(xform.GridUid.Value, out _))
            return;

        if (comp.NextDepartureTime.HasValue && _timing.CurTime < comp.NextDepartureTime.Value)
            return;

        switch (comp.SelectedDestination)
        {
            case LavalandShuttleDestination.Station:
                DepartToStation(xform.GridUid.Value, shuttleComp);
                break;
            case LavalandShuttleDestination.Lavaland:
                DepartToLavaland(xform.GridUid.Value, shuttleComp, comp);
                break;
            default:
                return;
        }

        comp.NextDepartureTime = _timing.CurTime + comp.DepartureCooldown;
        SendState(uid, comp);
    }

    private void DepartToStation(EntityUid gridUid, ShuttleComponent shuttleComp)
    {
        EntityUid? stationGrid = null;
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var stationUid, out _))
        {
            stationGrid = _station.GetLargestGrid(stationUid);
            if (stationGrid != null)
                break;
        }

        if (stationGrid == null)
            return;

        _shuttle.FTLToDock(gridUid, shuttleComp, stationGrid.Value, startupTime: 0f, hyperspaceTime: 0f);
    }

    private void DepartToLavaland(EntityUid gridUid, ShuttleComponent shuttleComp, LavalandShuttleConsoleComponent comp)
    {
        if (comp.RecyclingOutpostGrid == null)
            return;

        var lavalandMapUid = Transform(comp.RecyclingOutpostGrid.Value).MapUid;
        if (lavalandMapUid == null)
            return;

        var targetCoords = new EntityCoordinates(lavalandMapUid.Value, new Vector2(-16f, -3f));
        _shuttle.FTLToCoordinates(gridUid, shuttleComp, targetCoords, Angle.Zero, startupTime: 0f, hyperspaceTime: 0f);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        var shuttleUid = args.Entity;
        var consoleQuery = EntityQueryEnumerator<LavalandShuttleConsoleComponent, TransformComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out var console, out var xform))
        {
            if (xform.GridUid == shuttleUid)
            {
                SendState(consoleUid, console);
                break;
            }
        }
    }

    public void SendState(EntityUid uid, LavalandShuttleConsoleComponent comp)
    {
        if (!_uiSystem.HasUi(uid, LavalandShuttleConsoleUiKey.Key))
            return;

        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
            return;

        var inFtl = HasComp<FTLComponent>(xform.GridUid.Value);
        var cooldownReady = !comp.NextDepartureTime.HasValue || _timing.CurTime >= comp.NextDepartureTime.Value;

        var canDepart = !inFtl && cooldownReady && comp.SelectedDestination != LavalandShuttleDestination.None;
        if (comp.SelectedDestination == LavalandShuttleDestination.Lavaland && comp.RecyclingOutpostGrid == null)
            canDepart = false;

        _uiSystem.SetUiState(uid, LavalandShuttleConsoleUiKey.Key,
            new LavalandShuttleConsoleBoundUserInterfaceState
            {
                SelectedDestination = comp.SelectedDestination,
                StationAvailable = true,
                LavalandAvailable = comp.RecyclingOutpostGrid != null,
                CanDepart = canDepart,
            });
    }
}
