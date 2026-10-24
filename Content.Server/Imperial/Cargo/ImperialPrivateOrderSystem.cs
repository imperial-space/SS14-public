using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Imperial.Cargo;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cargo;

/// <summary>
/// Личные заказы TG (self_paid у /obj/machinery/computer/cargo и goodies.dm): покупка со своего счёта,
/// маленькие товары (ORDER_GOODY) только так, ящик или кейс запирается на покупателя.
/// </summary>
public sealed class ImperialPrivateOrderSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ImperialBankSystem _bank = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public static readonly ProtoId<CargoMarketPrototype> Goody = "ImperialCargoGoody";

    /// <summary>Прототип ящика → покупатели, чьи заказы ещё не выданы.</summary>
    private readonly Dictionary<string, Queue<(int Account, string Holder)>> _pending = new();
    private static readonly ProtoId<CargoAccountPrototype> CargoAccount = "Cargo";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialPrivateOrderConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<ImperialPrivateOrderConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<ImperialPrivateOrderConsoleComponent, ImperialPrivateOrderBuyMessage>(OnBuy);

        SubscribeLocalEvent<ImperialOwnedLockComponent, LockToggleAttemptEvent>(OnLockToggle);
        SubscribeLocalEvent<ImperialCargoPackComponent, MapInitEvent>(OnPackMapInit);
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ => _pending.Clear());
        SubscribeLocalEvent<ImperialOwnedLockComponent, ExaminedEvent>(OnLockExamined);
    }

    /// <summary>Вызывается при старте консоли карго: добавляет окно личных заказов.</summary>
    public void Setup(EntityUid console)
    {
        EnsureComp<ImperialPrivateOrderConsoleComponent>(console);
        _ui.SetUi(console, ImperialPrivateOrderUiKey.Key, new InterfaceData("ImperialPrivateOrderBoundUserInterface"));
    }

    private void OnVerbs(Entity<ImperialPrivateOrderConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-private-order-verb"),
            Act = () => _ui.OpenUi(ent.Owner, ImperialPrivateOrderUiKey.Key, user),
        });
    }

    private void OnOpened(Entity<ImperialPrivateOrderConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent, args.Actor);
    }

    /// <summary>Наборы рынка консоли и маленькие товары.</summary>
    private IEnumerable<CargoProductPrototype> Packs()
    {
        return _proto.EnumeratePrototypes<CargoProductPrototype>()
            .Where(p => p.Group == ImperialCargoCatalogSystem.Market || p.Group == Goody);
    }

    private void UpdateUi(EntityUid console, EntityUid user, string? message = null)
    {
        var account = _bank.AccountOf(user);
        var packs = Packs().Select(p => new ImperialExpressPack
        {
            Id = p.ID,
            Name = p.Name,
            Description = p.Description,
            Category = Loc.GetString(p.Category),
            Cost = p.Cost,
        }).OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();

        message ??= account == null
            ? Loc.GetString("imperial-private-order-no-account")
            : Loc.GetString("imperial-private-order-hint");

        _ui.SetUiState(console, ImperialPrivateOrderUiKey.Key,
            new ImperialPrivateOrderState(account?.Holder ?? string.Empty, account?.Balance ?? 0, message, packs));
    }

    private void OnBuy(Entity<ImperialPrivateOrderConsoleComponent> ent, ref ImperialPrivateOrderBuyMessage args)
    {
        Purchase(ent, args.Actor, args.Pack);
    }

    public void Purchase(Entity<ImperialPrivateOrderConsoleComponent> ent, EntityUid user, string packId)
    {
        if (_timing.CurTime < ent.Comp.NextOrder)
            return;

        if (!_proto.TryIndex<CargoProductPrototype>(packId, out var pack) || !Packs().Contains(pack))
            return;

        // self_paid: «ID-карта не обнаружена» / «неверный счёт».
        if (_bank.AccountOf(user) is not { } account)
        {
            UpdateUi(ent, user, Loc.GetString("imperial-private-order-no-account"));
            return;
        }

        if (account.Balance < pack.Cost)
        {
            UpdateUi(ent, user, Loc.GetString("imperial-private-order-no-money"));
            return;
        }

        if (_station.GetOwningStation(ent) is not { } station
            || !TryComp<StationCargoOrderDatabaseComponent>(station, out var db)
            || !TryComp<StationDataComponent>(station, out var stationData))
            return;

        ent.Comp.NextOrder = _timing.CurTime + TimeSpan.FromSeconds(1);
        _bank.Adjust(account, -pack.Cost, Loc.GetString("imperial-bank-reason-cargo", ("pack", pack.Name)));

        // Владелец ждёт в очереди, пока ящик не появится (сразу на торговой станции или позже с телепада).
        if (!_pending.TryGetValue(pack.Product.Id, out var queue))
            _pending[pack.Product.Id] = queue = new Queue<(int, string)>();
        queue.Enqueue((account.Id, account.Holder));

        _cargo.AddAndApproveOrder(station, pack, 1, account.Holder,
            Loc.GetString("imperial-private-order-description"), Name(ent), db, CargoAccount, (station, stationData));

        UpdateUi(ent, user, Loc.GetString("imperial-private-order-done", ("pack", pack.Name)));
    }

    /// <summary>crate/secure/owned: «название — куплено Имя».</summary>
    private void OnPackMapInit(Entity<ImperialCargoPackComponent> ent, ref MapInitEvent args)
    {
        if (MetaData(ent).EntityPrototype?.ID is not { } proto
            || !_pending.TryGetValue(proto, out var queue) || !queue.TryDequeue(out var owner))
            return;

        var owned = EnsureComp<ImperialOwnedLockComponent>(ent);
        owned.AccountId = owner.Account;
        owned.Holder = owner.Holder;
        var lockComp = EnsureComp<LockComponent>(ent);
        _lock.Lock(ent, null, lockComp);
        _meta.SetEntityName(ent, Loc.GetString("imperial-private-order-crate-name", ("name", Name(ent)), ("holder", owner.Holder)));
    }

    /// <summary>Замок открывает только карта владельца счёта.</summary>
    private void OnLockToggle(Entity<ImperialOwnedLockComponent> ent, ref LockToggleAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var account = _bank.AccountOf(args.User);
        if (account?.Id == ent.Comp.AccountId)
            return;

        args.Cancelled = true;
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("imperial-private-order-not-owner", ("holder", ent.Comp.Holder)), ent, args.User);
    }

    private void OnLockExamined(Entity<ImperialOwnedLockComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-private-order-examine", ("holder", ent.Comp.Holder)));
    }
}
