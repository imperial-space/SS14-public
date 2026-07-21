using Robust.Shared.Containers;
using Robust.Shared.Map;
using System.Numerics;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.DeviceLinking.Events;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.KAKTYC.GridConsole;

public sealed class GridConsoleSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridConsoleComponent, SignalReceivedEvent>(SignalReceived);
        SubscribeLocalEvent<GridConsoleComponent, ExaminedEvent>(ConsoleExamined);
    }
    public void SignalReceived(EntityUid uid, GridConsoleComponent component, SignalReceivedEvent args)
    {
        if (component.IsBlock)
        {
            _popup.PopupPredicted(Loc.GetString("teleport-is-blocked-message"), uid, uid, type: PopupType.MediumCaution);
            return;
        }
        var containerSlotOne = _container.GetContainer(uid, component.DiskContainer);
        var coords = _transform.GetMapCoordinates(uid);
        _mapManager.TryFindGridAt(coords, out var uidGrid, out var grid);
        foreach (var disk in containerSlotOne.ContainedEntities)
            if (TryComp<TeleportDiskComponent>(disk, out var diskComp))
            {
                var entCoords = Transform(uid).Coordinates;
                var xCoord = diskComp.PosX;
                var yCoord = diskComp.PosY;
                component.TargetPosX = xCoord;
                component.TargetPosY = yCoord;
                component.TargetMap = diskComp.FloatId;
                var pos = new EntityCoordinates(diskComp.MapId, new Vector2(xCoord, yCoord));
                _audio.PlayPredicted(component.EmpSound, entCoords, uid);
                _transform.SetCoordinates(uidGrid, pos);
            }
    }
    private void ConsoleExamined(EntityUid uid, GridConsoleComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString($"grid-console-line", ("numberX", component.TargetPosX), ("numberY", component.TargetPosY), ("mapID", component.TargetMap)));
    }
}
