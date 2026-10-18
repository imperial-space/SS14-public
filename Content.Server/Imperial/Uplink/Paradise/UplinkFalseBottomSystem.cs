using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Uplink.Paradise;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Uplink.Paradise;

/// <summary>
/// Кейс с ложным дном (Paradise, storage/briefcase/false_bottomed): отвёрткой открывается тайник на один предмет.
/// Если в тайнике оружие, кейс стреляет им.
/// </summary>
public sealed class UplinkFalseBottomSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    private static readonly ProtoId<ItemSizePrototype> MaxStoredSize = "Normal";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UplinkFalseBottomComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<UplinkFalseBottomComponent, InteractUsingEvent>(OnInteractUsing, before: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<UplinkFalseBottomComponent, InteractHandEvent>(OnInteractHand, before: [typeof(SharedItemSystem)]);
        SubscribeLocalEvent<UplinkFalseBottomComponent, UplinkFalseBottomDoAfterEvent>(OnPryDoAfter);
        SubscribeLocalEvent<UplinkFalseBottomComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<UplinkFalseBottomComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInit(Entity<UplinkFalseBottomComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
    }

    private void OnExamined(Entity<UplinkFalseBottomComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Open && args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("uplink-false-bottom-examine-open"));
    }

    private ContainerSlot GetSlot(Entity<UplinkFalseBottomComponent> ent)
    {
        return _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
    }

    /// <summary>screwdriver_act() и attackby() при открытом дне.</summary>
    private void OnInteractUsing(Entity<UplinkFalseBottomComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_tool.HasQuality(args.Used, "Screwing"))
        {
            args.Handled = true;
            if (ent.Comp.Open)
            {
                ent.Comp.Open = false;
                var stored = GetSlot(ent).ContainedEntity;
                _popup.PopupEntity(stored != null
                        ? Loc.GetString("uplink-false-bottom-close-item", ("item", stored.Value))
                        : Loc.GetString("uplink-false-bottom-close"),
                    args.User,
                    args.User);
                return;
            }

            if (ent.Comp.Busy)
                return;

            ent.Comp.Busy = true;
            _popup.PopupEntity(Loc.GetString("uplink-false-bottom-hunting"), args.User, args.User);
            if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.PryTime, new UplinkFalseBottomDoAfterEvent(), ent, ent, args.Used)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                }))
                ent.Comp.Busy = false;

            return;
        }

        if (!ent.Comp.Open)
            return;

        args.Handled = true;
        var slot = GetSlot(ent);
        if (slot.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("uplink-false-bottom-full"), args.User, args.User);
            return;
        }

        if (!TryComp<ItemComponent>(args.Used, out var item)
            || _item.GetSizePrototype(item.Size) > _proto.Index(MaxStoredSize))
        {
            _popup.PopupEntity(Loc.GetString("uplink-false-bottom-too-big", ("item", args.Used)), args.User, args.User);
            return;
        }

        if (!_container.Insert(args.Used, slot))
            return;

        _popup.PopupEntity(Loc.GetString("uplink-false-bottom-placed", ("item", args.Used)), args.User, args.User);
    }

    private void OnPryDoAfter(Entity<UplinkFalseBottomComponent> ent, ref UplinkFalseBottomDoAfterEvent args)
    {
        ent.Comp.Busy = false;
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Open = true;
        _popup.PopupEntity(Loc.GetString("uplink-false-bottom-opened"), args.User, args.User);
    }

    /// <summary>attack_hand(): при открытом дне рука достаёт спрятанное, а не берёт кейс.</summary>
    private void OnInteractHand(Entity<UplinkFalseBottomComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !ent.Comp.Open || GetSlot(ent).ContainedEntity is not { } stored)
            return;

        args.Handled = true;
        _container.Remove(stored, GetSlot(ent));
        _hands.PickupOrDrop(args.User, stored);
        _popup.PopupEntity(Loc.GetString("uplink-false-bottom-taken", ("item", stored)), args.User, args.User);
    }

    /// <summary>afterattack(): кейс стреляет оружием из тайника.</summary>
    private void OnShotAttempted(Entity<UplinkFalseBottomComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
        if (GetSlot(ent).ContainedEntity is not { } stored
            || !TryComp<GunComponent>(stored, out var gun)
            || args.Used.Comp.ShootCoordinates is not { } coords)
            return;

        _gun.AttemptShoot(args.User, (stored, gun), coords);
    }
}
