using System;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Hull;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;

namespace Content.Shared.Imperial.Medieval.Ships.Repairing;

public sealed class ShipRepairSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedShipHullSystem _shipHull = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RepairMaterialComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<RepairMaterialComponent, RepairUseEvent>(OnRepairUse);
    }

    private void OnAfterInteract(EntityUid uid, RepairMaterialComponent component, AfterInteractEvent args)
    {
        var playerEntity = args.User;
        if (args.Handled || !args.CanReach)
            return;

        var boat = _transform.GetParentUid(playerEntity);
        var clickEntity = args.ClickLocation.EntityId;
        if (boat != clickEntity)
            return;

        if (!TryComp<MapGridComponent>(boat, out var boatComponent))
            return;

        if (!_map.TryGetTileRef(boat, boatComponent, args.ClickLocation, out var tile))
            return;

        if (!_shipHull.TryGetPreviousDamageTile(tile.Tile.TypeId, out _))
            return;

        _popup.PopupClient("Ты начинаешь закрывать дыры доской", playerEntity);
        var time = 7 - _skills.GetSkillLevel(playerEntity, "Agility") * 0.05f - _skills.GetSkillLevel(playerEntity, "Intelligence") * 0.25f;
        time = Math.Max(1.0f, time);

        var doAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new RepairUseEvent(tile.GridIndices),
            args.Used,
            boat,
            args.Used)
        {
            MovementThreshold = 0.1f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnRepairUse(EntityUid uid, RepairMaterialComponent component, RepairUseEvent args)
    {
        if (args.Cancelled || args.Target is null)
            return;

        if (!TryComp<MapGridComponent>(args.Target, out var mapGrid))
            return;

        if (!_map.TryGetTileRef(args.Target.Value, mapGrid, args.TileCoordinates, out var currentTile) || currentTile.Tile.IsEmpty)
            return;

        if (!_shipHull.TryGetPreviousDamageTile(currentTile.Tile.TypeId, out var repairedTile))
            return;

        _map.SetTile(args.Target.Value, mapGrid, args.TileCoordinates, _shipHull.WithTileType(currentTile.Tile, repairedTile));
        _stack.Use(uid, 1);
        args.Handled = true;

        if (_net.IsServer)
            _audio.PlayPvs(MedievalShipSounds.HammerUse, uid);
    }
}
