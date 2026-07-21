using System;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Imperial.Medieval.Ships.WaterPump.Bucket;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Map.Components;

namespace Content.Shared.Imperial.Medieval.Ships.WaterPump;

public sealed class WaterPumpBucketSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly SharedWaterOnShipSystem _waterOnShip = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WaterPumpBucketComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<WaterPumpBucketComponent, BucketUseEvent>(OnBucketUse);
    }

    private void OnAfterInteract(EntityUid uid, WaterPumpBucketComponent component, AfterInteractEvent args)
    {
        var playerEntity = args.User;
        if (args.Handled || !args.CanReach)
            return;

        var boat = _transform.GetParentUid(playerEntity);
        var clickEntity = args.ClickLocation.EntityId;
        if (boat != clickEntity)
            return;

        if (!HasComp<MapGridComponent>(boat) || !HasComp<ShipDrowningComponent>(boat))
            return;

        _popup.PopupClient("Ты вычёрпываешь воду с корабля", playerEntity);
        var time = 7 - _skills.GetSkillLevel(playerEntity, "Agility") * 0.3f;
        time = Math.Max(1.0f, time);

        var doAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new BucketUseEvent(),
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

    private void OnBucketUse(EntityUid uid, WaterPumpBucketComponent component, BucketUseEvent args)
    {
        if (args.Cancelled || args.Target is null || args.Handled)
            return;

        _waterOnShip.RemoveWater(args.Target.Value, component.WaterCount);
        args.Repeat = true;
        args.Handled = true;
    }
}
