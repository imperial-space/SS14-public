using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;

namespace Content.Server.Imperial.KAKTYC.GridConsole;

public sealed class TeleportDiskSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TeleportDiskComponent, UseInHandEvent>(UseInHand);
        SubscribeLocalEvent<TeleportDiskComponent, ExaminedEvent>(DiskExamined);
    }
    public void UseInHand(EntityUid uid, TeleportDiskComponent component, UseInHandEvent args)
    {
        if (component.IsAlreadyUsed)
            return;
        var coords = _transform.GetMapCoordinates(uid);
        var mapId = Transform(uid).MapID;
        var id = _mapSystem.GetMap(mapId);
        component.FloatId = (float)mapId;
        component.MapId = id;
        component.PosX = coords.X;
        component.PosY = coords.Y;
        _popup.PopupPredicted(Loc.GetString($"teleport-disk-message", ("PosX", coords.X), ("PosY", coords.Y), ("Map", mapId)), uid, uid, type: PopupType.SmallCaution);
        component.IsAlreadyUsed = true;
    }
    private void DiskExamined(EntityUid uid, TeleportDiskComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString($"disk-coords", ("numberX", component.PosX), ("numberY", component.PosY), ("mapID", component.FloatId)));
    }
}
