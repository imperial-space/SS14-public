using System.Linq;
using Content.Server.Imperial.ImperialStore;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Server.Store.Components;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Stacks;
using Content.Shared.Store.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Trading;

public partial class TradingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly PopupSystem _popup = default!;


    [ViewVariables]
    public List<Guild> Guilds = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TradingComponent, ActivatableUIOpenAttemptEvent>(OnStoreOpenAttempt);
        SubscribeLocalEvent<MedievalCurrencyComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<TradingComponent, BeforeActivatableUIOpenEvent>(BeforeActivatableUiOpen);

        SubscribeLocalEvent<TradingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStart);

        InitializeUi();
    }

    private void OnRoundStart(RoundStartedEvent args)
    {
        CreateGuilds();
    }

    private void OnMapInit(EntityUid uid, TradingComponent component, MapInitEvent args)
    {
        RefreshAllGuilds(component);
    }

    private void OnStoreOpenAttempt(EntityUid uid, TradingComponent component, ActivatableUIOpenAttemptEvent args)
    {

        if (!component.OwnerOnly)
            return;

        component.AccountOwner ??= args.User;
        DebugTools.Assert(component.AccountOwner != null);

        if (component.AccountOwner == args.User)
            return;

        _popup.PopupEntity(Loc.GetString("store-not-account-owner", ("store", uid)), uid, args.User);
        args.Cancel();
    }

    private void OnAfterInteract(EntityUid uid, MedievalCurrencyComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;
        if (!TryComp<TradingComponent>(args.Target, out var store))
            return;
        if (!TryAddCurrency((uid, component), (args.Target.Value, store)))
            return;

        args.Handled = true;
        var msg = Loc.GetString("store-currency-inserted", ("used", args.Used), ("target", args.Target));
        _popup.PopupEntity(msg, args.Target.Value, args.User);
    }


    public bool TryAddCurrency(Entity<MedievalCurrencyComponent?> currency, Entity<TradingComponent?> store)
    {
        if (!Resolve(currency.Owner, ref currency.Comp))
            return false;

        if (!Resolve(store.Owner, ref store.Comp))
            return false;

        var value = currency.Comp.Price;
        if (TryComp(currency.Owner, out StackComponent? stack) && stack.Count != 1)
        {
            value = currency.Comp.Price
                .ToDictionary(v => v.Key, p => p.Value * stack.Count);
        }

        if (!TryAddCurrency(value, store, store.Comp))
            return false;

        currency.Comp.Price.Clear();
        if (stack != null)
            _stack.SetCount(currency.Owner, 0, stack);

        QueueDel(currency);
        return true;
    }

    public bool TryAddCurrency(Dictionary<string, FixedPoint2> currency, EntityUid uid, TradingComponent? store = null)
    {
        if (!Resolve(uid, ref store))
            return false;

        foreach (var type in currency)
        {
            if (store.Currency != type.Key)
                return false;
        }

        foreach (var (type, value) in currency)
        {
            store.Balance += value.Int();
        }

        UpdateUserInterface(null, uid, store);
        return true;
    }



    private void CreateGuilds()
    {
        Guilds = _prototypeManager.EnumeratePrototypes<GuildTypePrototype>()
            .SelectMany(gt => Enumerable.Range(0, gt.MaximumGuilds)
                .Select(_ => new Guild(gt, _robustRandom, _prototypeManager)))
            .ToList();
    }
}
