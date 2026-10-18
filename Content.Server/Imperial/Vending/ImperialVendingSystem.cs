using System.Linq;
using Content.Server.Cargo.Systems;
using Content.Server.Imperial.Cargo;
using Content.Server.Station.Systems;
using Content.Server.Vocalization.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Dataset;
using Content.Server.Stack;
using Content.Shared.Cargo.Components;
using Content.Shared.Destructible;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Vending;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Content.Shared.VendingMachines;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Vending;

/// <summary>
/// Покупки в торгоматах как в TG (code/modules/vending): цена товара (custom_price или default_price,
/// премиум и контрабанда — extra_price), оплата со счёта или наличными, скидка 80% для своего отдела,
/// деньги уходят в бюджет отдела автомата. Ассортимент автоматов с аналогом в TG заменяется на ассортимент TG.
/// </summary>
public sealed class ImperialVendingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedVendingMachineSystem _vending = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly ImperialBankSystem _bank = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly ProtoId<StackPrototype> Credit = "Credit";
    private static readonly SoundSpecifier CashRegister = new SoundPathSpecifier("/Audio/Imperial/Cargo/cashregister.ogg");

    /// <summary>DEPARTMENT_DISCOUNT.</summary>
    private const float DepartmentDiscount = 0.2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VendingMachineComponent, ComponentStartup>(OnVendingStartup);
        SubscribeLocalEvent<ImperialVendingComponent, MapInitEvent>(OnMapInit, after: new[] { typeof(SharedVendingMachineSystem) });
        SubscribeLocalEvent<ImperialVendingComponent, RestockDoAfterEvent>(OnRestock, after: new[] { typeof(SharedVendingMachineSystem) });
        SubscribeLocalEvent<ImperialVendingComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ImperialVendingComponent, ImperialVendingBuyMessage>(OnBuy);
        SubscribeLocalEvent<ImperialVendingComponent, BreakageEventArgs>(OnBreak);
    }

    private ImperialVendingProfilePrototype? Profile(VendingMachineComponent vending)
    {
        return _proto.TryIndex<ImperialVendingProfilePrototype>(vending.PackPrototypeId, out var profile) ? profile : null;
    }

    /// <summary>Окно автомата с ценами вместо ванильного.</summary>
    private void OnVendingStartup(Entity<VendingMachineComponent> ent, ref ComponentStartup args)
    {
        if (Profile(ent.Comp) is not { } profile)
            return;

        var comp = EnsureComp<ImperialVendingComponent>(ent);
        comp.Free = profile.Free;
        comp.Department = profile.Department;
        comp.DefaultPrice = profile.DefaultPrice;
        comp.ExtraPrice = profile.ExtraPrice;
        _ui.SetUi(ent.Owner, ImperialVendingUiKey.Key, new InterfaceData("ImperialVendingBoundUserInterface"));
        if (TryComp<ActivatableUIComponent>(ent, out var activatable))
            activatable.Key = ImperialVendingUiKey.Key;
    }

    private void OnMapInit(Entity<ImperialVendingComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<VendingMachineComponent>(ent, out var vending) || Profile(vending) is not { } profile)
            return;

        // onstation: автомат не на станции раздаёт бесплатно.
        if (_station.GetOwningStation(ent) == null)
            ent.Comp.Free = true;

        Fill(ent, vending, profile);
    }

    /// <summary>build_inventories(): ассортимент TG на полную вместимость.</summary>
    private void Fill(Entity<ImperialVendingComponent> ent, VendingMachineComponent vending, ImperialVendingProfilePrototype profile)
    {
        ent.Comp.Prices.Clear();
        ent.Comp.Premium.Clear();
        ent.Comp.Categories.Clear();

        if (profile.Replace)
        {
            vending.Inventory.Clear();
            vending.ContrabandInventory.Clear();

            foreach (var product in profile.Products)
            {
                if (!_proto.HasIndex<EntityPrototype>(product.Id))
                    continue;

                var type = product.Type == ImperialVendingProductType.Contraband ? InventoryType.Contraband : InventoryType.Regular;
                var inventory = type == InventoryType.Contraband ? vending.ContrabandInventory : vending.Inventory;
                var key = ImperialVendingComponent.Key(type, product.Id);

                if (inventory.TryGetValue(product.Id, out var existing))
                {
                    existing.Amount += product.Amount;
                    continue;
                }

                inventory[product.Id] = new VendingMachineInventoryEntry(type, product.Id, product.Amount);
                ent.Comp.Prices[key] = product.Price;
                if (product.Type == ImperialVendingProductType.Premium)
                    ent.Comp.Premium.Add(key);
                if (product.Category != null)
                    ent.Comp.Categories[key] = product.Category;
            }
        }

        Dirty(ent);
        Dirty(ent.Owner, vending);
    }

    /// <summary>Канистра пополнения (refill_canister) восстанавливает ассортимент TG.</summary>
    private void OnRestock(Entity<ImperialVendingComponent> ent, ref RestockDoAfterEvent args)
    {
        if (args.Cancelled || !TryComp<VendingMachineComponent>(ent, out var vending) || Profile(vending) is not { Replace: true } profile)
            return;

        Fill(ent, vending, profile);
    }

    private void OnUiOpened(Entity<ImperialVendingComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!ImperialVendingUiKey.Key.Equals(args.UiKey))
            return;

        UpdateUserState(ent, args.Actor);
    }

    private void UpdateUserState(Entity<ImperialVendingComponent> ent, EntityUid user)
    {
        // ui_data(): user — владелец ID-карты, cash — счёт и наличные, ad — случайная строка product_ads.
        var account = _bank.AccountOf(user);
        var hasCard = _idCard.TryFindIdCard(user, out var card);
        var holder = hasCard ? card.Comp.FullName ?? string.Empty : account?.Holder ?? string.Empty;
        var job = hasCard ? card.Comp.LocalizedJobTitle ?? string.Empty : string.Empty;
        var cash = (account?.Balance ?? 0) + CashOf(user).Sum(e => Comp<StackComponent>(e).Count);

        var ad = string.Empty;
        if (TryComp<DatasetVocalizerComponent>(ent, out var vocalizer)
            && _proto.TryIndex(vocalizer.Dataset, out var dataset) && dataset.Values.Count > 0)
            ad = Loc.GetString(_random.Pick(dataset.Values));

        _ui.SetUiState(ent.Owner, ImperialVendingUiKey.Key,
            new ImperialVendingUserState(hasCard || account != null, holder, job, cash, account?.Department?.Id, ad, ent.Comp.Free));
    }

    private void OnBreak(Entity<ImperialVendingComponent> ent, ref BreakageEventArgs args)
    {
        _ui.CloseUi(ent.Owner, ImperialVendingUiKey.Key);
    }

    private void OnBuy(Entity<ImperialVendingComponent> ent, ref ImperialVendingBuyMessage args)
    {
        Buy(ent, args.Actor, args.Type, args.Id);
    }

    public void Buy(Entity<ImperialVendingComponent> ent, EntityUid user, InventoryType type, string id)
    {
        if (!TryComp<VendingMachineComponent>(ent, out var vending))
            return;

        var entry = _vending.GetAllInventory(ent, vending).FirstOrDefault(e => e.Type == type && e.ID == id);
        if (entry == null || entry.Amount == 0 || vending.Ejecting || vending.Broken || !_power.IsPowered(ent.Owner))
            return;

        if (!_vending.IsAuthorized(ent, user, vending))
            return;

        var price = ent.Comp.PriceOf(type, id);
        if (price > 0 && !HasComp<BorgChassisComponent>(user) && !Pay(ent, user, price, id, ent.Comp.Premium.Contains(ImperialVendingComponent.Key(type, id))))
        {
            _vending.Deny((ent, vending), user);
            return;
        }

        _vending.TryEjectVendorItem(ent, type, id, vending.CanShoot, user, vending);
        UpdateUserState(ent, user);
    }

    /// <summary>proceed_payment() и компонент оплаты TG: сначала счёт, затем наличные.</summary>
    private bool Pay(Entity<ImperialVendingComponent> ent, EntityUid user, int price, string itemId, bool discountless)
    {
        var name = _proto.TryIndex<EntityPrototype>(itemId, out var proto) ? proto.Name : itemId;
        var account = _bank.AccountOf(user);

        if (!discountless && account != null && ent.Comp.Department != null && account.Department == ent.Comp.Department)
            price = Math.Max((int) Math.Round(price * DepartmentDiscount), 1);

        if (account != null && _bank.Adjust(account, -price, Loc.GetString("imperial-vending-reason", ("vendor", Name(ent)))))
        {
            Deposit(ent, price);
            _bank.CardTalk(account, Loc.GetString("imperial-vending-charged", ("price", price)));
            _audio.PlayPvs(CashRegister, ent);
            return true;
        }

        if (PayCash(user, price))
        {
            Deposit(ent, price);
            _popup.PopupEntity(Loc.GetString("imperial-vending-cash"), ent, user);
            _audio.PlayPvs(CashRegister, ent);
            return true;
        }

        _popup.PopupEntity(Loc.GetString(account == null ? "imperial-vending-no-account" : "imperial-vending-no-funds", ("item", name)), ent, user);
        return false;
    }

    /// <summary>Деньги уходят в бюджет отдела автомата.</summary>
    private void Deposit(EntityUid vendor, int price)
    {
        if (TryComp<ImperialVendingComponent>(vendor, out var comp) && comp.Department is { } department
            && _station.GetOwningStation(vendor) is { } station && TryComp<StationBankAccountComponent>(station, out var bank))
            _cargo.UpdateBankAccount((station, bank), price, department);
    }

    public List<EntityUid> CashOf(EntityUid user)
    {
        return _inventory.GetHandOrInventoryEntities(user)
            .Where(e => TryComp<StackComponent>(e, out var stack) && stack.StackTypeId == Credit)
            .ToList();
    }

    /// <summary>handle_cardless(): наличные в руках и карманах, сдача кредитами.</summary>
    public bool PayCash(EntityUid user, int price)
    {
        var cash = CashOf(user);

        var total = cash.Sum(e => Comp<StackComponent>(e).Count);
        if (total < price)
            return false;

        var left = price;
        foreach (var bill in cash)
        {
            var stack = Comp<StackComponent>(bill);
            var take = Math.Min(stack.Count, left);
            left -= take;
            if (take >= stack.Count)
                QueueDel(bill);
            else
                _stack.ReduceCount((bill, stack), take);

            if (left <= 0)
                break;
        }

        return true;
    }
}
