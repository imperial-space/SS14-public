using System.Linq;
using Content.Server.Cargo.Components;
using Content.Shared.Cargo;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Labels.Components;
using Content.Shared.Paper;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    private const string WeeklyCargoCratePrototype = "CrateGenericSteel";
    private const string WeeklyCargoCrateContainerId = "entity_storage";

    private void OnWeeklyCargoCatalogChanged(WeeklyCargoCatalogChangedEvent args)
    {
        UpdateAllOrderConsoles();
    }

    private static CargoOrderData GetOrderData(CargoConsoleAddOrderMessage args, WeeklyCargoProductData cargoProduct, int id, ProtoId<CargoAccountPrototype> account)
    {
        return new CargoOrderData(id, cargoProduct, args.Amount, args.Requester, args.Reason, account);
    }

    private void UpdateAllOrderConsoles()
    {
        var orderQuery = AllEntityQuery<CargoOrderConsoleComponent>();

        while (orderQuery.MoveNext(out var uid, out _))
        {
            var station = _station.GetOwningStation(uid);
            UpdateOrderState(uid, station);
        }
    }

    private bool FulfillWeeklyOrder(CargoOrderData order, ProtoId<CargoAccountPrototype> account, EntityCoordinates spawn, string? paperProto)
    {
        if (order.WeeklyProduct is not { } product)
            return false;

        if (product.Amount < 1 || string.IsNullOrWhiteSpace(product.ItemPrototype))
            return false;

        EntityUid item;
        if (product.Boxed)
        {
            var containerEntity = Spawn(WeeklyCargoCratePrototype, spawn);
            _transformSystem.Unanchor(containerEntity, Transform(containerEntity));

            if (!_container.TryGetContainer(containerEntity, WeeklyCargoCrateContainerId, out var container))
            {
                QueueDel(containerEntity);
                return false;
            }

            var spawnedItems = new List<EntityUid>();
            for (var i = 0; i < product.Amount; i++)
            {
                var child = Spawn(product.ItemPrototype, spawn);
                spawnedItems.Add(child);
                _transformSystem.Unanchor(child, Transform(child));

                if (_container.Insert(child, container, force: true))
                    continue;

                foreach (var spawned in spawnedItems)
                    QueueDel(spawned);

                QueueDel(containerEntity);
                return false;
            }

            item = containerEntity;
        }
        else
        {
            EntityUid? firstItem = null;
            for (var i = 0; i < product.Amount; i++)
            {
                var child = Spawn(product.ItemPrototype, spawn);
                _transformSystem.Unanchor(child, Transform(child));
                firstItem ??= child;
            }

            if (firstItem == null)
                return false;

            item = firstItem.Value;
        }

        PrintCargoOrderPaper(item, order, account, spawn, paperProto, product.Name);
        return true;
    }

    private void PrintCargoOrderPaper(
        EntityUid item,
        CargoOrderData order,
        ProtoId<CargoAccountPrototype> account,
        EntityCoordinates spawn,
        string? paperProto,
        string itemName)
    {
        var printed = Spawn(paperProto, spawn);
        if (TryComp<PaperComponent>(printed, out var paper))
        {
            var val = Loc.GetString("cargo-console-paper-print-name", ("orderNumber", order.OrderId));
            _metaSystem.SetEntityName(printed, val);

            var accountProto = _protoMan.Index(account);
            _paperSystem.SetContent((printed, paper),
                Loc.GetString(
                    "cargo-console-paper-print-text",
                    ("orderNumber", order.OrderId),
                    ("itemName", itemName),
                    ("orderQuantity", order.OrderQuantity),
                    ("requester", order.Requester),
                    ("reason", string.IsNullOrWhiteSpace(order.Reason) ? Loc.GetString("cargo-console-paper-reason-default") : order.Reason),
                    ("account", Loc.GetString(accountProto.Name)),
                    ("accountcode", Loc.GetString(accountProto.Code)),
                    ("approver", string.IsNullOrWhiteSpace(order.Approver) ? Loc.GetString("cargo-console-paper-approver-default") : order.Approver)));

            if (TryComp<PaperLabelComponent>(item, out var label))
                _slots.TryInsert(item, label.LabelSlot, printed, null);
        }
    }

    public List<WeeklyCargoProductData> GetAvailableWeeklyProducts(Entity<CargoOrderConsoleComponent> ent)
    {
        if (_station.GetOwningStation(ent) is not { } station ||
            !TryComp<StationCargoOrderDatabaseComponent>(station, out var db))
        {
            return new List<WeeklyCargoProductData>();
        }

        if (!_weeklyMode.TryGetActiveWeeklyCargoProducts(out var products))
            return new List<WeeklyCargoProductData>();

        var markets = ent.Comp.AllowedGroups.Intersect(db.Markets).ToList();
        if (!markets.Contains("market"))
            return new List<WeeklyCargoProductData>();

        return products;
    }
}
