using System.Linq;
using Content.Shared.Imperial.Uplink.Components;
using Content.Server.Imperial.Uplink.Conditions;
using Content.Server.Store.Conditions;
using Content.Server.Store.Systems;
using Content.Server.StoreDiscount.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Uplink.Systems;

/// <summary>
/// Каталог аплинка в стиле TG: подменяет категории ванильных аплинков (предатели, ядерщики, ящики излишков),
/// раздаёт распродажи и обрабатывает «случайный предмет».
/// </summary>
public sealed class ImperialUplinkSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    /// <summary>Рации ядерных оперативников из ванильного снаряжения.</summary>
    private static readonly HashSet<string> NukeOpsRadios = new() { "BaseUplinkRadio40TC", "BaseUplinkRadio60TC" };
    private static readonly ProtoId<TagPrototype> NukeOpsTag = "NukeOpsUplink";
    private static readonly ProtoId<CurrencyPrototype> Telecrystal = "Telecrystal";
    private static readonly ProtoId<DiscountCategoryPrototype> NoDiscount = "ImperialUplinkNoDiscount";
    private static readonly ProtoId<ListingPrototype> RandomItemListing = "ImperialUplinkRandomItem";

    private static readonly ProtoId<StoreCategoryPrototype> Discounts = "ImperialUplinkDiscounts";
    private static readonly ProtoId<StoreCategoryPrototype> TeamDiscounts = "ImperialUplinkTeamDiscounts";
    private static readonly ProtoId<StoreCategoryPrototype> TeamLimitedDiscounts = "ImperialUplinkTeamLimitedDiscounts";

    private const string SaleModifier = "ImperialUplinkSale";

    /// <summary>uplink_sales_min/max у предателя.</summary>
    private const int TraitorSalesMin = 4;
    private const int TraitorSalesMax = 6;

    /// <summary>TRAITOR_DISCOUNT_MIN_PRICE.</summary>
    private const int TraitorSaleMinPrice = 4;

    /// <summary>discount_team_amount и discount_limited_amount у ядерщиков.</summary>
    private const int TeamSales = 5;
    private const int TeamLimitedSales = 10;

    private static readonly ProtoId<StoreCategoryPrototype>[] CatalogCategories =
    {
        "ImperialUplinkWeaponKits",
        "ImperialUplinkNukeAmmo",
        "ImperialUplinkReinforcements",
        "ImperialUplinkMech",
        "ImperialUplinkBundles",
        "ImperialUplinkContractor",
        "ImperialUplinkDangerous",
        "ImperialUplinkStealthy",
        "ImperialUplinkAmmo",
        "ImperialUplinkExplosives",
        "ImperialUplinkStealthyTools",
        "ImperialUplinkDeviceTools",
        "ImperialUplinkSuits",
        "ImperialUplinkImplants",
        "ImperialUplinkRoleRestricted",
        "ImperialUplinkSpeciesRestricted",
        "ImperialUplinkBadassery",
        Discounts,
        TeamDiscounts,
        TeamLimitedDiscounts,
        "UplinkObjective",
    };

    private static readonly HashSet<ProtoId<StoreCategoryPrototype>> SaleCategories = new()
    {
        Discounts,
        TeamDiscounts,
        TeamLimitedDiscounts,
    };

    /// <summary>Командные скидки ядерщиков общие на весь раунд, как nuke_team.team_discounts.</summary>
    private List<ImperialUplinkSale>? _teamSales;

    public override void Initialize()
    {
        base.Initialize();

        EntityManager.EntityInitialized += OnEntityInitialized;

        SubscribeLocalEvent<StoreInitializedEvent>(OnStoreInitialized, after: [typeof(StoreDiscountSystem)]);
        SubscribeLocalEvent<ImperialUplinkComponent, MapInitEvent>(OnMapInit, after: [typeof(StoreSystem)]);
        SubscribeLocalEvent<ImperialUplinkComponent, StoreBuyListingMessage>(OnBuyRequest, before: [typeof(StoreSystem)]);
        SubscribeLocalEvent<ImperialUplinkComponent, StoreRequestRefundMessage>(OnRefund, after: [typeof(StoreSystem)]);
        SubscribeLocalEvent<StoreBuyFinishedEvent>(OnBuyFinished);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _teamSales = null);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        EntityManager.EntityInitialized -= OnEntityInitialized;
    }

    public bool IsNukeOpsStore(EntityUid? store)
    {
        return store != null && _tag.HasTag(store.Value, NukeOpsTag);
    }

    /// <summary>Сколько раз куплены позиции с общим ключом лимита на этом аплинке.</summary>
    public int GetSharedStockPurchases(EntityUid? store, string key)
    {
        if (store == null || !TryComp<StoreComponent>(store, out var comp))
            return 0;

        var total = 0;
        foreach (var listing in comp.FullListingsCatalog)
        {
            if (listing.Conditions == null)
                continue;

            foreach (var condition in listing.Conditions)
            {
                if (condition is ImperialUplinkSharedStockCondition shared && shared.Key == key)
                {
                    total += listing.PurchaseAmount;
                    break;
                }
            }
        }

        return total;
    }

    /// <summary>
    /// Рации ядерщиков (ванильные 40/60 ТК) и свои прототипы с <see cref="ImperialUplinkComponent"/> получают каталог TG.
    /// Остальные ванильные аплинки (ящики излишков, отладочные рации и т.д.) не трогаем.
    /// </summary>
    private void OnEntityInitialized(Entity<MetaDataComponent> ent)
    {
        if (!HasComp<StoreComponent>(ent))
            return;

        if (HasComp<ImperialUplinkComponent>(ent)
            || ent.Comp.EntityPrototype is { } proto && NukeOpsRadios.Contains(proto.ID))
            ConvertStore(ent);
    }

    private void ConvertStore(EntityUid uid)
    {
        if (!TryComp<StoreComponent>(uid, out var store))
            return;

        store.Categories.Clear();
        foreach (var category in CatalogCategories)
        {
            store.Categories.Add(category);
        }

        EnsureComp<ImperialUplinkComponent>(uid);
    }

    /// <summary>
    /// Аплинк агента (UplinkSystem.SetUplink: предатель, имплант, adduplink) получает каталог TG
    /// и распродажу create_uplink_sales(rand(4, 6), discounts).
    /// </summary>
    private void OnStoreInitialized(ref StoreInitializedEvent ev)
    {
        if (!TryComp<StoreComponent>(ev.Store, out var store))
            return;

        if (!store.Categories.Contains(Discounts))
            ConvertStore(ev.Store);

        if (!ev.UseDiscounts
            || !TryComp<ImperialUplinkComponent>(ev.Store, out var uplink)
            || uplink.SalesRolled)
            return;

        uplink.SalesRolled = true;
        uplink.Sales = RollSales(ev.Store,
            store,
            ev.TargetUser,
            _random.Next(TraitorSalesMin, TraitorSalesMax + 1),
            Discounts,
            false,
            TraitorSaleMinPrice,
            null);

        ApplySales((ev.Store, uplink, store));
    }

    /// <summary>Командные скидки ядерщиков: 5 обычных и 10 ограниченных, одинаковые для всей команды.</summary>
    private void OnMapInit(Entity<ImperialUplinkComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.SalesRolled || !IsNukeOpsStore(ent) || !TryComp<StoreComponent>(ent, out var store))
            return;

        ent.Comp.SalesRolled = true;

        if (_teamSales == null)
        {
            _teamSales = RollSales(ent, store, ent, TeamSales, TeamDiscounts, false, 2, null);
            var taken = _teamSales.Select(s => s.ListingId).ToHashSet();
            _teamSales.AddRange(RollSales(ent, store, ent, TeamLimitedSales, TeamLimitedDiscounts, true, 2, taken));
        }

        foreach (var sale in _teamSales)
        {
            ent.Comp.Sales.Add(new ImperialUplinkSale
            {
                ListingId = sale.ListingId,
                Category = sale.Category,
                Reduction = sale.Reduction,
                Percent = sale.Percent,
                Limited = sale.Limited,
            });
        }

        ApplySales((ent, ent.Comp, store));
    }

    private List<ImperialUplinkSale> RollSales(
        EntityUid storeUid,
        StoreComponent store,
        EntityUid buyer,
        int count,
        ProtoId<StoreCategoryPrototype> saleCategory,
        bool limited,
        int minPrice,
        HashSet<string>? exclude)
    {
        var perCategory = new Dictionary<ProtoId<StoreCategoryPrototype>, List<ListingDataWithCostModifiers>>();
        foreach (var listing in store.FullListingsCatalog)
        {
            if (listing.DiscountCategory == NoDiscount || exclude?.Contains(listing.ID) == true)
                continue;

            if (!listing.OriginalCost.TryGetValue(Telecrystal, out var cost) || cost < minPrice)
                continue;

            ProtoId<StoreCategoryPrototype>? category = null;
            foreach (var cat in listing.Categories)
            {
                if (!SaleCategories.Contains(cat) && CatalogCategories.Contains(cat))
                {
                    category = cat;
                    break;
                }
            }

            if (category == null || !SaleConditionsMet(listing, storeUid, buyer))
                continue;

            if (!perCategory.TryGetValue(category.Value, out var list))
            {
                list = new List<ListingDataWithCostModifiers>();
                perCategory[category.Value] = list;
            }

            list.Add(listing);
        }

        var result = new List<ImperialUplinkSale>();
        var categories = perCategory.Keys.ToList();
        for (var i = 0; i < count && categories.Count > 0; i++)
        {
            var category = _random.PickAndTake(categories);
            var listing = _random.Pick(perCategory[category]);
            var cost = listing.OriginalCost[Telecrystal].Int();

            var discount = RollDiscount();
            if (cost >= 20)
                discount *= 0.5f;

            var newCost = Math.Max((int) MathF.Floor(cost * (1f - discount) + 0.5f), 1);
            if (newCost >= cost)
                continue;

            result.Add(new ImperialUplinkSale
            {
                ListingId = listing.ID,
                Category = saleCategory,
                Reduction = cost - newCost,
                Percent = (int) MathF.Floor((cost - newCost) * 100f / cost + 0.5f),
                Limited = limited,
            });
        }

        return result;
    }

    /// <summary>get_discount: 25% (вес 4), 50% (вес 2), 75% (вес 1).</summary>
    private float RollDiscount()
    {
        var roll = _random.Next(7);
        if (roll < 4)
            return 0.25f;

        return roll < 6 ? 0.5f : 0.75f;
    }

    /// <summary>
    /// Для распродажи важны только purchasable_from, профессия и раса — репутация, онлайн и лимиты не учитываются.
    /// </summary>
    private bool SaleConditionsMet(ListingDataWithCostModifiers listing, EntityUid store, EntityUid buyer)
    {
        if (listing.Conditions == null)
            return true;

        var args = new ListingConditionArgs(buyer, store, listing, EntityManager);
        foreach (var condition in listing.Conditions)
        {
            if (condition is ImperialUplinkProgressionCondition
                or ImperialUplinkPopulationCondition
                or ImperialUplinkSharedStockCondition
                or ListingLimitedStockCondition)
                continue;

            if (!condition.Condition(args))
                return false;
        }

        return true;
    }

    private void ApplySales(Entity<ImperialUplinkComponent, StoreComponent> ent)
    {
        foreach (var sale in ent.Comp1.Sales)
        {
            if (sale.Spent)
                continue;

            var listing = ent.Comp2.FullListingsCatalog.FirstOrDefault(l => l.ID == sale.ListingId);
            if (listing == null)
                continue;

            listing.RemoveCostModifier(SaleModifier);
            listing.AddCostModifier(SaleModifier, new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> { { Telecrystal, -sale.Reduction } });
            listing.Categories.Add(sale.Category);
        }
    }

    /// <summary>«Случайный предмет»: подменяем покупку на случайную доступную позицию, которую можно оплатить.</summary>
    private void OnBuyRequest(Entity<ImperialUplinkComponent> ent, ref StoreBuyListingMessage args)
    {
        if (args.Listing != RandomItemListing || !TryComp<StoreComponent>(ent, out var store))
            return;

        var buyer = store.AccountOwner ?? args.Actor;
        var possible = _store.GetAvailableListings(buyer, ent, store)
            .Where(l => l.ID != RandomItemListing.Id && l.ProductEntity != null && l.CanBuyWith(store.Balance))
            .ToList();

        if (possible.Count == 0)
            return;

        args.Listing = _random.Pick(possible).ID;
    }

    /// <summary>Возврат пересоздаёт каталог — восстанавливаем распродажи.</summary>
    private void OnRefund(Entity<ImperialUplinkComponent> ent, ref StoreRequestRefundMessage args)
    {
        if (!TryComp<StoreComponent>(ent, out var store))
            return;

        foreach (var sale in ent.Comp.Sales)
        {
            sale.Spent = false;
        }

        ApplySales((ent, ent.Comp, store));
    }

    /// <summary>Ограниченная командная скидка действует на одну покупку.</summary>
    private void OnBuyFinished(ref StoreBuyFinishedEvent ev)
    {
        if (!TryComp<ImperialUplinkComponent>(ev.StoreUid, out var uplink))
            return;

        foreach (var sale in uplink.Sales)
        {
            if (!sale.Limited || sale.Spent || sale.ListingId != ev.PurchasedItem.ID)
                continue;

            sale.Spent = true;
            ev.PurchasedItem.RemoveCostModifier(SaleModifier);
            ev.PurchasedItem.Categories.Remove(sale.Category);
        }
    }
}
