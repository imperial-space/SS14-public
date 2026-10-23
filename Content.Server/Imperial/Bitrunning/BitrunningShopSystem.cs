using System.Linq;
using Content.Server.Imperial.Cargo;
using Content.Server.Lightning;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Торгомат NexaCache (objects/vendor.dm, orders/*.dm).</summary>
[RegisterComponent]
public sealed partial class BitrunningVendorComponent : Component
{
    /// <summary>cargo_cost_multiplier: доставка грузовым рейсом дешевле на 35%.</summary>
    [DataField]
    public float CargoMultiplier = 0.65f;

    /// <summary>Сколько ждать заказ «грузовым рейсом».</summary>
    [DataField]
    public TimeSpan CargoDelay = TimeSpan.FromMinutes(2);

    [ViewVariables]
    public List<(EntProtoId Item, TimeSpan At)> Pending = new();

    [ViewVariables]
    public string? LastMessage;
}

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
        SubscribeLocalEvent<BitrunningVendorComponent, BitrunningVendorBuyMessage>(OnVendorBuy);

        SubscribeLocalEvent<BitrunningDiskComponent, GetVerbsEvent<AlternativeVerb>>(OnDiskVerbs);
        SubscribeLocalEvent<BitrunningDiskComponent, Content.Shared.Examine.ExaminedEvent>(OnDiskExamined);

        SubscribeLocalEvent<BitrunningSummonCheeseActionEvent>(OnSummonCheese);
        SubscribeLocalEvent<BitrunningLesserHealActionEvent>(OnLesserHeal);
        SubscribeLocalEvent<BitrunningLightningBoltActionEvent>(OnLightningBolt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BitrunningVendorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var vendor, out var xform))
        {
            for (var i = vendor.Pending.Count - 1; i >= 0; i--)
            {
                if (now < vendor.Pending[i].At)
                    continue;

                var item = vendor.Pending[i].Item;
                vendor.Pending.RemoveAt(i);
                Spawn(item, xform.Coordinates);
                _audio.PlayPvs(BlinkSound, uid);
                Spawn("EffectSparks", xform.Coordinates);
            }
        }
    }

    #region Очки

    public int GetPoints(EntityUid user)
    {
        return _bank.AccountOf(user) is { } account ? _points.GetValueOrDefault(account.Id) : 0;
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

    private void OnVendorOpened(Entity<BitrunningVendorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateVendorUi(ent, args.Actor);
    }

    private void UpdateVendorUi(Entity<BitrunningVendorComponent> ent, EntityUid user)
    {
        var account = _bank.AccountOf(user);
        var entries = _proto.EnumeratePrototypes<BitrunningOrderPrototype>()
            .Where(o => _proto.HasIndex<EntityPrototype>(o.Item))
            .OrderBy(o => o.Category)
            .ThenBy(o => o.Cost)
            .Select(o =>
            {
                var proto = _proto.Index<EntityPrototype>(o.Item);
                return new BitrunningVendorEntry(
                    o.ID,
                    proto.Name,
                    o.Description is { } desc ? Loc.GetString(desc) : proto.Description,
                    o.Category,
                    o.Cost,
                    (int) MathF.Ceiling(o.Cost * ent.Comp.CargoMultiplier));
            })
            .ToList();

        _ui.SetUiState(ent.Owner, BitrunningVendorUiKey.Key,
            new BitrunningVendorState(account?.Holder, account != null ? _points.GetValueOrDefault(account.Id) : 0, entries, ent.Comp.LastMessage));
    }

    /// <summary>order_groceries(): экспресс сразу на торгомат, грузовой рейс — дешевле и через время.</summary>
    private void OnVendorBuy(Entity<BitrunningVendorComponent> ent, ref BitrunningVendorBuyMessage args)
    {
        var user = args.Actor;
        if (!_proto.TryIndex<BitrunningOrderPrototype>(args.Id, out var order) || !_proto.HasIndex<EntityPrototype>(order.Item))
            return;

        if (_bank.AccountOf(user) is not { } account)
        {
            ent.Comp.LastMessage = Loc.GetString("bitrunning-vendor-no-account");
            _audio.PlayPvs(DenySound, ent);
            UpdateVendorUi(ent, user);
            return;
        }

        var cost = args.Express ? order.Cost : (int) MathF.Ceiling(order.Cost * ent.Comp.CargoMultiplier);
        var points = _points.GetValueOrDefault(account.Id);
        if (points < cost)
        {
            ent.Comp.LastMessage = Loc.GetString("bitrunning-vendor-not-enough");
            _audio.PlayPvs(DenySound, ent);
            UpdateVendorUi(ent, user);
            return;
        }

        _points[account.Id] = points - cost;
        _audio.PlayPvs(BuySound, ent);
        if (args.Express)
        {
            Spawn(order.Item, Transform(ent).Coordinates);
            _audio.PlayPvs(BlinkSound, ent);
            ent.Comp.LastMessage = Loc.GetString("bitrunning-vendor-express-done");
        }
        else
        {
            ent.Comp.Pending.Add((order.Item, _timing.CurTime + ent.Comp.CargoDelay));
            ent.Comp.LastMessage = Loc.GetString("bitrunning-vendor-cargo-done");
        }

        UpdateVendorUi(ent, user);
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
