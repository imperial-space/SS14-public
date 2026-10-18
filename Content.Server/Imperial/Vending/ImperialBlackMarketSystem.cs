using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Cargo;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Vending;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Vending;

/// <summary>
/// Чёрный рынок TG (SSmarket, market_uplink): общий на раунд ассортимент со случайными ценой и запасом,
/// оплата со счёта, доставка запуском из космоса (бесплатно) или телепортом в случайное место станции (+75, через 60 с).
/// </summary>
public sealed class ImperialBlackMarketSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ImperialBankSystem _bank = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public const int LaunchFee = 0;
    public const int TeleportFee = 75;

    private static readonly EntProtoId LaunchCrate = "CrateGenericSteel";
    private static readonly SoundSpecifier BuySound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");
    private static readonly SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");
    private static readonly TimeSpan TeleportDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RestockInterval = TimeSpan.FromMinutes(20);

    private sealed class Stock
    {
        public int Price;
        public int Amount;
    }

    private readonly Dictionary<string, Stock> _market = new();
    private TimeSpan _nextRestock;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _market.Clear());
        SubscribeLocalEvent<ImperialMarketUplinkComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<ImperialMarketUplinkComponent, ImperialMarketBuyMessage>(OnBuy);
    }

    /// <summary>market_item/New(): цена rand(min, max), запас rand(min, max), товар есть с вероятностью availability_prob.</summary>
    private void EnsureMarket()
    {
        if (_market.Count > 0)
            return;

        foreach (var item in _proto.EnumeratePrototypes<ImperialMarketItemPrototype>())
        {
            var amount = _random.Prob(item.Availability / 100f) ? _random.Next(item.StockMin, item.StockMax + 1) : 0;
            _market[item.ID] = new Stock { Price = _random.Next(item.PriceMin, Math.Max(item.PriceMin, item.PriceMax) + 1), Amount = amount };
        }

        _nextRestock = _timing.CurTime + RestockInterval;
    }

    /// <summary>market/restock(): товар с запасом ниже максимума пополняется с вероятностью availability_prob.</summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_market.Count == 0 || _timing.CurTime < _nextRestock)
            return;

        _nextRestock = _timing.CurTime + RestockInterval;
        foreach (var (id, stock) in _market)
        {
            if (!_proto.TryIndex<ImperialMarketItemPrototype>(id, out var item) || stock.Amount >= item.StockMax || !_random.Prob(item.Availability / 100f))
                continue;

            stock.Amount += _random.Next(1, item.StockMax - stock.Amount + 1);
        }
    }

    private void OnOpened(Entity<ImperialMarketUplinkComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent, args.Actor);
    }

    private void UpdateUi(EntityUid uplink, EntityUid user)
    {
        EnsureMarket();
        var account = _bank.AccountOf(user);
        var items = new List<ImperialMarketEntry>();
        foreach (var (id, stock) in _market)
        {
            if (stock.Amount <= 0 || !_proto.TryIndex<ImperialMarketItemPrototype>(id, out var item))
                continue;

            items.Add(new ImperialMarketEntry(id, item.Item, item.Name, item.Description, item.Category, stock.Price, stock.Amount));
        }

        _ui.SetUiState(uplink, ImperialMarketUiKey.Key,
            new ImperialMarketState(account?.Holder ?? string.Empty, account?.Balance ?? 0, LaunchFee, TeleportFee, items.OrderBy(i => i.Category).ThenBy(i => i.Name).ToList()));
    }

    /// <summary>market/purchase(): цена товара + доставка, списание со счёта.</summary>
    private void OnBuy(Entity<ImperialMarketUplinkComponent> ent, ref ImperialMarketBuyMessage args)
    {
        EnsureMarket();
        var user = args.Actor;
        if (!_market.TryGetValue(args.Id, out var stock) || stock.Amount <= 0 || !_proto.TryIndex<ImperialMarketItemPrototype>(args.Id, out var item))
            return;

        var account = _bank.AccountOf(user);
        if (account == null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-market-no-account"), ent, user);
            return;
        }

        var price = stock.Price + (args.Shipping == ImperialMarketShipping.Teleport ? TeleportFee : LaunchFee);
        if (!_bank.Adjust(account, -price, Loc.GetString("imperial-market-reason")))
        {
            _popup.PopupEntity(Loc.GetString("imperial-market-no-funds", ("item", item.Name)), ent, user);
            return;
        }

        stock.Amount--;
        _audio.PlayEntity(BuySound, user, ent);

        if (args.Shipping == ImperialMarketShipping.Teleport)
            Teleport(ent, user, item);
        else
            Launch(ent, user, item);

        UpdateUi(ent, user);
    }

    /// <summary>SHIPPING_METHOD_LAUNCH: ящик выбрасывается из космоса с случайной стороны в сторону аплинка.</summary>
    private void Launch(EntityUid uplink, EntityUid user, ImperialMarketItemPrototype item)
    {
        var target = _transform.GetMapCoordinates(uplink);
        var dirs = new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY };
        var dir = _random.Pick(dirs);
        var start = new MapCoordinates(target.Position + dir * 40f, target.MapId);

        var crate = Spawn(LaunchCrate, start);
        var goods = Spawn(item.Item, start);
        if (TryComp<Content.Shared.Storage.Components.EntityStorageComponent>(crate, out _))
            EntityManager.System<Content.Shared.Storage.EntitySystems.SharedEntityStorageSystem>().Insert(goods, crate);

        _throwing.TryThrow(crate, -dir * 40f, 6f, playSound: false);
        _popup.PopupEntity(Loc.GetString("imperial-market-launched", ("dir", Loc.GetString(DirName(-dir)))), user, user, PopupType.Medium);
    }

    private static string DirName(Vector2 dir)
    {
        return dir.X > 0.5f ? "imperial-market-dir-west"
            : dir.X < -0.5f ? "imperial-market-dir-east"
            : dir.Y > 0.5f ? "imperial-market-dir-south"
            : "imperial-market-dir-north";
    }

    /// <summary>SHIPPING_METHOD_TELEPORT: случайное место станции, 60 секунд, чтобы успеть первым.</summary>
    private void Teleport(EntityUid uplink, EntityUid user, ImperialMarketItemPrototype item)
    {
        if (!TryRandomStationTile(uplink, out var coords))
            coords = Transform(user).Coordinates;

        var pos = _transform.ToMapCoordinates(coords).Position;
        _popup.PopupEntity(Loc.GetString("imperial-market-teleporting", ("x", (int) pos.X), ("y", (int) pos.Y)), user, user, PopupType.Medium);

        Robust.Shared.Timing.Timer.Spawn(TeleportDelay, () =>
        {
            Spawn(item.Item, coords);
            Spawn("EffectSparks", coords);
            _audio.PlayPvs(TeleportSound, coords);
        });
    }

    private bool TryRandomStationTile(EntityUid uplink, out EntityCoordinates coords)
    {
        coords = default;
        if (_station.GetOwningStation(uplink) is not { } station || _station.GetLargestGrid(station) is not { } grid
            || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var tiles = _map.GetAllTiles(grid, gridComp).Where(t => !_turf.IsSpace(t) && !_turf.IsTileBlocked(t, CollisionGroup.Impassable)).ToList();
        if (tiles.Count == 0)
            return false;

        var tile = _random.Pick(tiles);
        coords = _map.GridTileToLocal(grid, gridComp, tile.GridIndices);
        return true;
    }
}
