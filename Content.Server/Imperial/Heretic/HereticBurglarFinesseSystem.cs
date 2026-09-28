using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticBurglarFinesseSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem       _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem     _hands     = default!;
    [Dependency] private readonly SharedPopupSystem     _popup     = default!;
    [Dependency] private readonly IRobustRandom         _random    = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, HereticBurglarFinesseActionEvent>(OnBurglarFinesse);
    }

    private void OnBurglarFinesse(EntityUid uid, HereticComponent comp, HereticBurglarFinesseActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var target = args.Target;

        if (!_inventory.TryGetSlotEntity(target, "back", out var backItem) || backItem == null)
        {
            _popup.PopupEntity(Loc.GetString("heretic-burglar-finesse-no-bag"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!_container.TryGetContainer(backItem.Value, StorageComponent.ContainerId, out var storageContainer))
        {
            _popup.PopupEntity(Loc.GetString("heretic-burglar-finesse-no-bag"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var items = storageContainer.ContainedEntities.ToList();
        if (items.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-burglar-finesse-empty-bag"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var item       = _random.Pick(items);
        var itemName   = MetaData(item).EntityName;
        var targetName = MetaData(target).EntityName;

        _container.Remove(item, storageContainer);
        _hands.TryPickupAnyHand(uid, item);

        _popup.PopupEntity(Loc.GetString("heretic-burglar-finesse-victim-msg"), target, target, PopupType.SmallCaution);
        _popup.PopupEntity(
            Loc.GetString("heretic-burglar-finesse-caster-msg", ("item", itemName), ("target", targetName)),
            uid, uid, PopupType.Medium);
    }
}
