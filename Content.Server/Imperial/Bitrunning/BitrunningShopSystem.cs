using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Cargo;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.Lightning;
using Content.Shared.Actions;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>/obj/machinery/computer/order_console/bitrunning (objects/vendor.dm, order_computer.dm).</summary>
[RegisterComponent]
public sealed partial class BitrunningVendorComponent : Component
{
    /// <summary>cargo_cost_multiplier: доставка грузовым рейсом дешевле на 35%.</summary>
    [DataField]
    public float CargoMultiplier = 0.65f;

    /// <summary>express_cost_multiplier.</summary>
    [DataField]
    public float ExpressMultiplier = 1f;

    /// <summary>cooldown_time.</summary>
    [DataField]
    public TimeSpan CooldownTime = TimeSpan.FromSeconds(10);

    /// <summary>CARGO_CRATE_VALUE: заказ рейсом не дешевле ящика.</summary>
    [DataField]
    public int CargoValue = 200;

    /// <summary>grocery_list: заказ → количество.</summary>
    [ViewVariables]
    public Dictionary<string, int> Cart = new();

    [ViewVariables]
    public TimeSpan NextOrder;

    [ViewVariables]
    public bool CooldownShown;

    [ViewVariables]
    public EntityUid? LastUser;
}

/// <summary>/datum/supply_pack/bitrunning: ящик доставки, наполняется содержимым корзины при появлении.</summary>
[RegisterComponent]
public sealed partial class BitrunningDeliveryCrateComponent : Component;

/// <summary>
/// Очки битраннинга (bank_account.bitrunning_points), торгомат, программы-диски и их загрузка в аватара (stock_gear),
/// заклинания дисков.
/// </summary>
public sealed class BitrunningShopSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ImperialBankSystem _bank = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedEntityStorageSystem _storage = default!;

    private static readonly SoundSpecifier BuySound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
    private static readonly SoundSpecifier BlinkSound = new SoundPathSpecifier("/Audio/Magic/blink.ogg");
    private static readonly SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/click.ogg");
    private static readonly SoundSpecifier CheeseSound = new SoundPathSpecifier("/Audio/Magic/staff_animation.ogg");
    private static readonly SoundSpecifier HealSound = new SoundPathSpecifier("/Audio/Magic/staff_healing.ogg");

    /// <summary>Очки по номеру банковского счёта.</summary>
    private readonly Dictionary<int, int> _points = new();

    private const int PointsPerReward = 100;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _points.Clear());
        SubscribeLocalEvent<BitrunningPilotComponent, BitrunningDomainCompletedEvent>(OnDomainCompleted);

        SubscribeLocalEvent<BitrunningVendorComponent, BoundUIOpenedEvent>(OnVendorOpened);
        SubscribeLocalEvent<BitrunningVendorComponent, BitrunningVendorCartMessage>(OnVendorCart);
        SubscribeLocalEvent<BitrunningVendorComponent, BitrunningVendorPurchaseMessage>(OnVendorPurchase);
        SubscribeLocalEvent<BitrunningDeliveryCrateComponent, MapInitEvent>(OnDeliveryCrateInit);

        SubscribeLocalEvent<BitrunningDiskComponent, GetVerbsEvent<AlternativeVerb>>(OnDiskVerbs);
        SubscribeLocalEvent<BitrunningDiskComponent, Content.Shared.Examine.ExaminedEvent>(OnDiskExamined);

        SubscribeLocalEvent<BitrunningSummonCheeseActionEvent>(OnSummonCheese);
        SubscribeLocalEvent<BitrunningLesserHealActionEvent>(OnLesserHeal);
        SubscribeLocalEvent<BitrunningLightningBoltActionEvent>(OnLightningBolt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Откат заказа закончился — окно снимает «Order sent!» (у tgui это autoupdate).
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BitrunningVendorComponent>();
        while (query.MoveNext(out var uid, out var vendor))
        {
            if (vendor.CooldownShown && now >= vendor.NextOrder)
            {
                vendor.CooldownShown = false;
                UpdateVendorUi((uid, vendor), vendor.LastUser);
            }
        }
    }

    #region Очки

    public int GetPoints(EntityUid user)
    {
        return _bank.AccountOf(user) is { } account ? _points.GetValueOrDefault(account.Id) : 0;
    }

    /// <summary>Админское начисление: очки на счёт того, чья карта у сущности. false — счёта нет.</summary>
    public bool AddPoints(EntityUid user, int amount)
    {
        if (_bank.AccountOf(user) is not { } account)
            return false;

        _points[account.Id] = Math.Max(0, _points.GetValueOrDefault(account.Id) + amount);
        return true;
    }

    /// <summary>netpod/on_domain_complete(): reward_points * 100 на счёт подключённого.</summary>
    private void OnDomainCompleted(Entity<BitrunningPilotComponent> ent, ref BitrunningDomainCompletedEvent args)
    {
        if (_bank.AccountOf(ent) is not { } account)
            return;

        _points[account.Id] = _points.GetValueOrDefault(account.Id) + args.Reward * PointsPerReward;
    }

    #endregion

    #region Торгомат

    /// <summary>order_categories: CATEGORY_BITRUNNING_FLAIR, CATEGORY_BITRUNNING_TECH.</summary>
    private static readonly string[] Categories = { "flair", "tech" };

    private const int MaxCartAmount = 20;

    private void OnVendorOpened(Entity<BitrunningVendorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateVendorUi(ent, args.Actor);
    }

    private IEnumerable<BitrunningOrderPrototype> Orders()
    {
        return _proto.EnumeratePrototypes<BitrunningOrderPrototype>()
            .Where(o => Categories.Contains(o.Category) && _proto.HasIndex<EntityPrototype>(o.Item));
    }

    private int TotalCost(BitrunningVendorComponent vendor)
    {
        var total = 0;
        foreach (var (id, amount) in vendor.Cart)
        {
            if (_proto.TryIndex<BitrunningOrderPrototype>(id, out var order))
                total += order.Cost * amount;
        }

        return total;
    }

    private void UpdateVendorUi(Entity<BitrunningVendorComponent> ent, EntityUid? user)
    {
        ent.Comp.LastUser = user;
        var points = user is { } u ? GetPoints(u) : 0;
        var entries = Orders()
            .OrderBy(o => o.Cost)
            .Select(o =>
            {
                var proto = _proto.Index<EntityPrototype>(o.Item);
                return new BitrunningVendorEntry(
                    o.ID,
                    o.Item,
                    proto.Name,
                    o.Description is { } desc ? Loc.GetString(desc) : proto.Description,
                    o.Category,
                    (int) MathF.Round(o.Cost * ent.Comp.CargoMultiplier));
            })
            .ToList();

        var categories = Categories.Select(c => Loc.GetString($"bitrunning-vendor-category-{c}")).ToList();
        foreach (var entry in entries)
        {
            entry.Category = Loc.GetString($"bitrunning-vendor-category-{entry.Category}");
        }

        _ui.SetUiState(ent.Owner, BitrunningVendorUiKey.Key, new BitrunningVendorState(
            points,
            categories,
            entries,
            new Dictionary<string, int>(ent.Comp.Cart),
            TotalCost(ent.Comp),
            _timing.CurTime >= ent.Comp.NextOrder,
            ent.Comp.CargoMultiplier,
            ent.Comp.ExpressMultiplier,
            ent.Comp.CargoValue));
    }

    /// <summary>add_one / remove_one / cart_set.</summary>
    private void OnVendorCart(Entity<BitrunningVendorComponent> ent, ref BitrunningVendorCartMessage args)
    {
        var id = args.Id;
        if (!Orders().Any(o => o.ID == id))
            return;

        var amount = Math.Clamp(args.Amount, 0, MaxCartAmount);
        if (amount == 0)
            ent.Comp.Cart.Remove(args.Id);
        else
            ent.Comp.Cart[args.Id] = amount;

        UpdateVendorUi(ent, args.Actor);
    }

    /// <summary>ui_act("purchase") / ui_act("express").</summary>
    private void OnVendorPurchase(Entity<BitrunningVendorComponent> ent, ref BitrunningVendorPurchaseMessage args)
    {
        var user = args.Actor;
        if (ent.Comp.Cart.Count == 0 || _timing.CurTime < ent.Comp.NextOrder)
            return;

        var total = TotalCost(ent.Comp);
        var cost = (int) MathF.Floor(total * (args.Express ? ent.Comp.ExpressMultiplier : ent.Comp.CargoMultiplier));

        // «Чтобы шахтёры не скупали ящики за бесценок».
        if (!args.Express && cost < ent.Comp.CargoValue)
            return;

        if (_bank.AccountOf(user) is not { } account)
        {
            Say(ent, Loc.GetString("bitrunning-vendor-no-account"));
            _audio.PlayPvs(DenySound, ent);
            return;
        }

        // subtract_points(): не хватает — ничего не происходит.
        var points = _points.GetValueOrDefault(account.Id);
        if (points < cost)
        {
            _audio.PlayPvs(DenySound, ent);
            return;
        }

        _points[account.Id] = points - cost;

        var items = new List<EntProtoId>();
        foreach (var (id, amount) in ent.Comp.Cart)
        {
            if (!_proto.TryIndex<BitrunningOrderPrototype>(id, out var order))
                continue;

            for (var i = 0; i < amount; i++)
            {
                items.Add(order.Item);
            }
        }

        ent.Comp.Cart.Clear();
        _audio.PlayPvs(BuySound, ent);

        if (args.Express)
        {
            // podspawn(): телепорт-капсула прямо к покупателю.
            var coords = Transform(user).Coordinates;
            foreach (var item in items)
            {
                Spawn(item, coords);
            }

            Spawn("EffectSparks", coords);
            _audio.PlayPvs(BlinkSound, user);
            Say(ent, Loc.GetString("bitrunning-vendor-express-done"));
            ent.Comp.NextOrder = _timing.CurTime + ent.Comp.CooldownTime * ent.Comp.ExpressMultiplier;
        }
        else
        {
            OrderDelivery(ent, user, items);
            ent.Comp.NextOrder = _timing.CurTime + ent.Comp.CooldownTime;
        }

        ent.Comp.CooldownShown = true;
        UpdateVendorUi(ent, user);
    }

    private readonly Queue<List<EntProtoId>> _deliveries = new();
    private static readonly ProtoId<CargoProductPrototype> DeliveryProduct = "CargoBitrunningDelivery";
    private static readonly ProtoId<CargoAccountPrototype> CargoAccount = "Cargo";
    private static readonly ProtoId<Content.Shared.Radio.RadioChannelPrototype> SupplyChannel = "Supply";

    /// <summary>order_groceries(): заказ без оплаты отдела, ящик прибудет грузовым рейсом.</summary>
    private void OrderDelivery(Entity<BitrunningVendorComponent> ent, EntityUid user, List<EntProtoId> items)
    {
        if (_station.GetOwningStation(ent) is not { } station
            || !TryComp<StationCargoOrderDatabaseComponent>(station, out var db)
            || !TryComp<StationDataComponent>(station, out var stationData)
            || !_proto.TryIndex(DeliveryProduct, out var product))
        {
            // Станции нет (тесты, шаттл) — заказ прилетает сразу.
            foreach (var item in items)
            {
                Spawn(item, Transform(ent).Coordinates);
            }

            return;
        }

        _deliveries.Enqueue(items);
        var name = Name(user);
        _cargo.AddAndApproveOrder(station, product, 1, name,
            Loc.GetString("bitrunning-vendor-order-reason"), Loc.GetString("bitrunning-vendor-orderer-rank"), db, CargoAccount, (station, stationData));

        Say(ent, Loc.GetString("bitrunning-vendor-cargo-done"));
        _radio.SendRadioMessage(ent, Loc.GetString("bitrunning-vendor-announce"), SupplyChannel, ent);
    }

    private void OnDeliveryCrateInit(Entity<BitrunningDeliveryCrateComponent> ent, ref MapInitEvent args)
    {
        if (!_deliveries.TryDequeue(out var items))
            return;

        var coords = Transform(ent).Coordinates;
        foreach (var item in items)
        {
            var spawned = Spawn(item, coords);
            _storage.Insert(spawned, ent);
        }
    }

    private void Say(EntityUid source, string message)
    {
        _chat.TrySendInGameICMessage(source, message, InGameICChatType.Speak, hideChat: false);
    }

    #endregion

    #region Диски

    private void OnDiskExamined(Entity<BitrunningDiskComponent> ent, ref Content.Shared.Examine.ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("bitrunning-disk-examine-carry"));
        if (ent.Comp.Choice is { } choice && choice < ent.Comp.Options.Count)
            args.PushMarkup(Loc.GetString("bitrunning-disk-examine-chosen", ("choice", Loc.GetString(ent.Comp.Options[choice].Name))));
        else
            args.PushMarkup(Loc.GetString("bitrunning-disk-examine-choose"));
    }

    /// <summary>attack_self(): выбор программы — один раз.</summary>
    private void OnDiskVerbs(Entity<BitrunningDiskComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Choice != null)
            return;

        var user = args.User;
        for (var i = 0; i < ent.Comp.Options.Count; i++)
        {
            if (ent.Comp.Options[i].Items.Any(p => !_proto.HasIndex<EntityPrototype>(p)))
                continue;

            var index = i;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(ent.Comp.Options[i].Name),
                Category = VerbCategory.SelectType,
                Act = () =>
                {
                    if (ent.Comp.Choice != null)
                        return;

                    ent.Comp.Choice = index;
                    Dirty(ent);
                    _audio.PlayPvs(ClickSound, ent);
                    _popup.PopupEntity(Loc.GetString("bitrunning-disk-chosen"), ent, user);
                },
            });
        }
    }

    /// <summary>
    /// stock_gear(): диски, которые битраннер несёт с собой, загружаются в нового аватара.
    /// Повторные способности не дублируются; домен может запрещать предметы или способности.
    /// </summary>
    public void StockGear(EntityUid neo, EntityUid avatar, BitrunningDomainPrototype domain)
    {
        var forbidItems = (domain.Flags & BitrunningDomainFlags.ForbidsItems) != 0;
        var forbidAbilities = (domain.Flags & BitrunningDomainFlags.ForbidsAbilities) != 0;
        if (forbidItems || forbidAbilities)
            _popup.PopupEntity(Loc.GetString("bitrunning-disk-domain-forbids"), neo, neo, PopupType.MediumCaution);

        var blocked = false;
        var failed = false;
        foreach (var disk in EnumerateCarried(neo))
        {
            if (!TryComp<BitrunningDiskComponent>(disk, out var comp))
                continue;

            if (comp.Choice is not { } choice || choice >= comp.Options.Count)
            {
                failed = true;
                continue;
            }

            var option = comp.Options[choice];
            if (option.Items.Count > 0)
            {
                if (forbidItems)
                {
                    blocked = true;
                }
                else
                {
                    foreach (var item in option.Items)
                    {
                        if (!_proto.HasIndex<EntityPrototype>(item))
                            continue;

                        var spawned = Spawn(item, Transform(avatar).Coordinates);
                        if (!_hands.TryPickupAnyHand(avatar, spawned) && !_inventory.TryEquip(avatar, spawned, "back", silent: true))
                            _transform.SetCoordinates(spawned, Transform(avatar).Coordinates);
                    }
                }
            }

            if (option.Actions.Count > 0)
            {
                if (forbidAbilities)
                {
                    blocked = true;
                    continue;
                }

                foreach (var action in option.Actions)
                {
                    if (_actions.GetActions(avatar).Any(a => MetaData(a.Owner).EntityPrototype?.ID == action.Id))
                    {
                        failed = true;
                        continue;
                    }

                    _actions.AddAction(avatar, action.Id);
                }
            }
        }

        if (failed)
            _popup.PopupEntity(Loc.GetString("bitrunning-disk-load-failed"), neo, neo, PopupType.MediumCaution);
        if (blocked)
            _popup.PopupEntity(Loc.GetString("bitrunning-disk-load-blocked"), neo, neo, PopupType.MediumCaution);
    }

    /// <summary>Всё, что битраннер несёт: руки, слоты и вложенные контейнеры.</summary>
    private IEnumerable<EntityUid> EnumerateCarried(EntityUid owner)
    {
        var stack = new Stack<EntityUid>();
        stack.Push(owner);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!TryComp<ContainerManagerComponent>(current, out var manager))
                continue;

            foreach (var container in _container.GetAllContainers(current, manager))
            {
                foreach (var inside in container.ContainedEntities)
                {
                    yield return inside;
                    stack.Push(inside);
                }
            }
        }
    }

    #endregion

    #region Заклинания дисков

    /// <summary>conjure/cheese: девять головок сыра вокруг.</summary>
    private void OnSummonCheese(BitrunningSummonCheeseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var coords = Transform(args.Performer).Coordinates;
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
        {
            Spawn("FoodCheese", coords.Offset(new System.Numerics.Vector2(x, y)));
        }

        _audio.PlayPvs(CheeseSound, args.Performer);
    }

    /// <summary>basic_heal: 10 грубого и 10 ожогов себе.</summary>
    private void OnLesserHeal(BitrunningLesserHealActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"] = -10f / 3f;
        heal.DamageDict["Slash"] = -10f / 3f;
        heal.DamageDict["Piercing"] = -10f / 3f;
        heal.DamageDict["Heat"] = -10f;
        _damage.TryChangeDamage(args.Performer, heal, ignoreResistances: true);
        _audio.PlayPvs(HealSound, args.Performer);
        _popup.PopupEntity(Loc.GetString("bitrunning-spell-heal"), args.Performer, args.Performer);
    }

    /// <summary>pointed/projectile/lightningbolt.</summary>
    private void OnLightningBolt(BitrunningLightningBoltActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _lightning.ShootLightning(args.Performer, args.Target);
    }

    #endregion
}
