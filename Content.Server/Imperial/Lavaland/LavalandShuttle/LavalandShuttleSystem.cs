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

namespace Content.Server.Imperial.Lavaland.LavalandShuttle;

public sealed class LavalandShuttleSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandShuttleConsoleComponent, AfterActivatableUIOpenEvent>(OnUIOpened);
        SubscribeLocalEvent<LavalandShuttleConsoleComponent, LavalandShuttleFlyToStationMessage>(OnFlyToStation);
        SubscribeLocalEvent<LavalandShuttleConsoleComponent, LavalandShuttleFlyToLavalandMessage>(OnFlyToLavaland);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
    }

    private void OnUIOpened(EntityUid uid, LavalandShuttleConsoleComponent comp, AfterActivatableUIOpenEvent args)
    {
        SendState(uid, comp);
    }

    private void OnFlyToStation(EntityUid uid, LavalandShuttleConsoleComponent comp, LavalandShuttleFlyToStationMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
            return;

        if (!TryComp(xform.GridUid.Value, out ShuttleComponent? shuttleComp))
            return;

        if (!_shuttle.CanFTL(xform.GridUid.Value, out _))
            return;

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

        _shuttle.FTLToDock(xform.GridUid.Value, shuttleComp, stationGrid.Value);
        SendState(uid, comp);
    }

    private void OnFlyToLavaland(EntityUid uid, LavalandShuttleConsoleComponent comp, LavalandShuttleFlyToLavalandMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
            return;

        if (!TryComp(xform.GridUid.Value, out ShuttleComponent? shuttleComp))
            return;

        if (comp.RecyclingOutpostGrid == null)
            return;

        if (!_shuttle.CanFTL(xform.GridUid.Value, out _))
            return;

        var lavalandMapUid = Transform(comp.RecyclingOutpostGrid.Value).MapUid;
        if (lavalandMapUid == null)
            return;

        var targetCoords = new EntityCoordinates(lavalandMapUid.Value, new Vector2(-16f, -3f));
        _shuttle.FTLToCoordinates(xform.GridUid.Value, shuttleComp, targetCoords, Angle.Zero);
        SendState(uid, comp);
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

        _uiSystem.SetUiState(uid, LavalandShuttleConsoleUiKey.Key,
            new LavalandShuttleConsoleBoundUserInterfaceState
            {
                CanFlyToStation = !inFtl,
                CanFlyToLavaland = !inFtl && comp.RecyclingOutpostGrid != null,
            });
    }
}
