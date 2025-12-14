
using Content.Shared.UserInterface;
using Content.Shared.FixedPoint;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Imperial.ImperialStore;
using Robust.Shared.Configuration;
using Content.Shared.Imperial.ICCVar;

namespace Content.Server.Imperial.ImperialStore;

/// <summary>
/// Manages general interactions with a store and different entities,
/// getting listings for stores, and interfacing with the store UI.
/// </summary>
public sealed partial class ImperialStoreSystem : SharedImperialStoreSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private TimeSpan _balanceUpdateTimer;
    public TimeSpan BalanceUpdateInterval = TimeSpan.FromSeconds(30);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialCurrencyComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ImperialStoreComponent, BeforeActivatableUIOpenEvent>(BeforeActivatableUiOpen);

        SubscribeLocalEvent<ImperialStoreComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImperialStoreComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ImperialStoreComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ImperialStoreComponent, OpenUplinkImplantEvent>(OnImplantActivate);

        InitializeUi();
        InitializeCommand();
        InitializeRefund();

        Subs.CVar(_cfg, ICCVars.StoreBalanceUpdateInterval, value => BalanceUpdateInterval = value, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _balanceUpdateTimer += TimeSpan.FromSeconds(frameTime);

        if (_balanceUpdateTimer <= BalanceUpdateInterval)
            return;

        _balanceUpdateTimer = TimeSpan.Zero;

        var query = EntityQueryEnumerator<ImperialStoreComponent>();
        while (query.MoveNext(out var uid, out var store))
        {
            store.Balance = CurrencySum(store.Balance, store.BonusSum);
            UpdateUserInterface(null, uid, store);
        }
    }

    private void OnMapInit(EntityUid uid, ImperialStoreComponent component, MapInitEvent args)
    {
        RefreshAllListings(component);
        component.StartingMap = Transform(uid).MapUid;
    }

    private void OnStartup(EntityUid uid, ImperialStoreComponent component, ComponentStartup args)
    {
        // for traitors, because the ImperialStoreComponent for the PDA can be added at any time.
        if (MetaData(uid).EntityLifeStage == EntityLifeStage.MapInitialized)
        {
            RefreshAllListings(component);
        }

        var ev = new ImperialStoreAddedEvent();
        RaiseLocalEvent(uid, ref ev, true);
    }

    private void OnShutdown(EntityUid uid, ImperialStoreComponent component, ComponentShutdown args)
    {
        var ev = new ImperialStoreRemovedEvent();
        RaiseLocalEvent(uid, ref ev, true);
    }

    private void OnAfterInteract(EntityUid uid, ImperialCurrencyComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!TryComp<ImperialStoreComponent>(args.Target, out var store))
            return;

        var ev = new CurrencyInsertAttemptEvent(args.User, args.Target.Value, args.Used, store);
        RaiseLocalEvent(args.Target.Value, ev);
        if (ev.Cancelled)
            return;

        //not adding to deposits to prevent duping
        args.Handled = TryAddCurrency(GetCurrencyValue(uid, component), args.Target.Value, store);

        if (args.Handled)
        {
            var msg = Loc.GetString("store-currency-inserted", ("used", args.Used), ("target", args.Target));
            _popup.PopupEntity(msg, args.Target.Value, args.User);
            QueueDel(args.Used);
        }
    }

    private void OnImplantActivate(EntityUid uid, ImperialStoreComponent component, OpenUplinkImplantEvent args)
    {
        ToggleUi(args.Performer, uid, component);
    }

    #region Public API

    public void BindMind(EntityUid uid, EntityUid newOwner, ImperialStoreComponent? component = null)
    {
        if (!Resolve(uid, ref component)) return;

        component.AccountOwner = newOwner;

        Dirty(uid, component);
    }

    /// <summary>
    /// Returns the sum of 'currency1' and 'currency2' (or their difference if 'subtract' is set).
    /// </summary>
    public Dictionary<string, FixedPoint2> CurrencySum(
        Dictionary<string, FixedPoint2> currency1,
        Dictionary<string, FixedPoint2> currency2,
        bool subtract = false)
    {
        Dictionary<string, FixedPoint2> result = [];
        IEnumerable<string> keys = currency1.Keys.Union(currency2.Keys);

        foreach (string currency in keys)
        {
            FixedPoint2 value1 = currency1.GetValueOrDefault(currency);
            FixedPoint2 value2 = currency2.GetValueOrDefault(currency);
            result[currency] = subtract ? value1 - value1 : value1 + value2;
        }

        return result;
    }

    /// <summary>
    /// Gets the value from an entity's currency component.
    /// Scales with stacks.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <returns>The value of the currency</returns>
    public Dictionary<string, FixedPoint2> GetCurrencyValue(EntityUid uid, ImperialCurrencyComponent component)
    {
        var amount = EntityManager.GetComponentOrNull<StackComponent>(uid)?.Count ?? 1;
        return component.Price.ToDictionary(v => v.Key, p => p.Value * amount);
    }

    //exists solely because whoever made store system couldn't settle on using just strings or just entprotoid
    public Dictionary<string, FixedPoint2> ConvertCurrency(Dictionary<EntProtoId, FixedPoint2> currency)
    {
        Dictionary<string, FixedPoint2> convertedCurrency = [];

        foreach ((EntProtoId k, FixedPoint2 v) in currency)
        {
            convertedCurrency[k.ToString()] = v;
        }

        return convertedCurrency;
    }

    /// <summary>
    /// Tries to add a currency to a store's balance.
    /// </summary>
    /// <param name="currencyEnt"></param>
    /// <param name="storeEnt"></param>
    /// <param name="currency">The currency to add</param>
    /// <param name="store">The store to add it to</param>
    /// <returns>Whether or not the currency was succesfully added</returns>
    [PublicAPI]
    public bool TryAddCurrency(EntityUid currencyEnt, EntityUid storeEnt, ImperialStoreComponent? store = null, ImperialCurrencyComponent? currency = null)
    {
        if (!Resolve(currencyEnt, ref currency) || !Resolve(storeEnt, ref store))
            return false;

        return TryAddCurrency(GetCurrencyValue(currencyEnt, currency), storeEnt, store);
    }

    public bool TryAddCurrency(Dictionary<EntProtoId, FixedPoint2> currency, EntityUid uid, ImperialStoreComponent? store = null)
    {
        return TryAddCurrency(ConvertCurrency(currency), uid, store);
    }

    /// <summary>
    /// Tries to add a currency to a store's balance
    /// </summary>
    /// <param name="currency">The value to add to the store</param>
    /// <param name="uid"></param>
    /// <param name="store">The store to add it to</param>
    /// <returns>Whether or not the currency was succesfully added</returns>
    public bool TryAddCurrency(Dictionary<string, FixedPoint2> currency, EntityUid uid, ImperialStoreComponent? store = null)
    {
        if (!Resolve(uid, ref store))
            return false;

        //verify these before values are modified
        foreach (var type in currency)
        {
            if (!store.CurrencyWhitelist.Contains(type.Key))
                return false;
        }

        store.Balance = CurrencySum(store.Balance, currency);
        UpdateUserInterface(null, uid, store);
        return true;
    }

    public bool TryAddBonus(Dictionary<EntProtoId, FixedPoint2> currency, EntityUid uid, ImperialStoreComponent? store = null)
    {
        return TryAddBonus(ConvertCurrency(currency), uid, store);
    }

    public bool TryAddBonus(Dictionary<string, FixedPoint2> currency, EntityUid uid, ImperialStoreComponent? store = null)
    {
        if (!Resolve(uid, ref store))
            return false;

        if (store.BonusCount > 0)
        {
            store.LastBonusIndex = --store.LastBonusIndex < 0 ? store.BonusCount - 1 : store.LastBonusIndex;
            store.Bonuses[store.LastBonusIndex] = currency;
            store.BonusSum.Clear();

            foreach (Dictionary<string, FixedPoint2> bonus in store.Bonuses)
            {
                if (bonus != null)
                    store.BonusSum = CurrencySum(store.BonusSum, bonus);
            }
        }

        return true;
    }

    #endregion
}

public sealed class CurrencyInsertAttemptEvent : CancellableEntityEventArgs
{
    public readonly EntityUid User;
    public readonly EntityUid Target;
    public readonly EntityUid Used;
    public readonly ImperialStoreComponent Store;

    public CurrencyInsertAttemptEvent(EntityUid user, EntityUid target, EntityUid used, ImperialStoreComponent store)
    {
        User = user;
        Target = target;
        Used = used;
        Store = store;
    }
}
