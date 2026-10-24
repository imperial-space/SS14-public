using System.Linq;
using Content.Server.Administration;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Imperial.Cargo;
using Content.Shared.Access.Components;
using Content.Shared.Chat;
using Content.Shared.Construction;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Vending;
using Content.Shared.Interaction;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Vending;

/// <summary>
/// vending/custom TG: владелец привязывает счёт картой или КПК, кладёт предметы с ценой,
/// деньги покупателей идут на его счёт. Разборка с привязанным счётом — взрыв.
/// </summary>
public sealed class ImperialCustomVendorSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ImperialBankSystem _bank = default!;
    [Dependency] private readonly ImperialVendingSystem _vending = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private static readonly SoundSpecifier VendSound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialCustomVendorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ImperialCustomVendorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ImperialCustomVendorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialCustomVendorComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ImperialCustomVendorComponent, ImperialCustomVendorBuyMessage>(OnBuy);
        SubscribeLocalEvent<ImperialCustomVendorComponent, ImperialCustomVendorPriceMessage>(OnPrice);
        SubscribeLocalEvent<ImperialCustomVendorComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ImperialCustomVendorComponent, MachineDeconstructedEvent>(OnDeconstructed);
    }

    private BaseContainer Storage(Entity<ImperialCustomVendorComponent> ent)
    {
        return _container.GetContainer(ent, ent.Comp.Container);
    }

    private void OnStartup(Entity<ImperialCustomVendorComponent> ent, ref ComponentStartup args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.Container);
    }

    private bool IsOwner(Entity<ImperialCustomVendorComponent> ent, EntityUid user)
    {
        return ent.Comp.Account is { } id && _bank.AccountOf(user) is { } account && account.Id == id;
    }

    private void OnInteractUsing(Entity<ImperialCustomVendorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        var used = args.Used;

        // Привязать / отвязать счёт картой или КПК.
        if (HasComp<IdCardComponent>(used) || HasComp<PdaComponent>(used))
        {
            args.Handled = true;
            var account = _bank.AccountOf(user);
            if (account == null)
            {
                _popup.PopupEntity(Loc.GetString("imperial-custom-vendor-no-account"), ent, user);
                return;
            }

            if (ent.Comp.Account == null)
            {
                ent.Comp.Account = account.Id;
                ent.Comp.Owner = account.Holder;
                _popup.PopupEntity(Loc.GetString("imperial-custom-vendor-linked"), ent, user);
            }
            else if (ent.Comp.Account == account.Id)
            {
                ent.Comp.Account = null;
                ent.Comp.Owner = string.Empty;
                _popup.PopupEntity(Loc.GetString("imperial-custom-vendor-unlinked"), ent, user);
            }
            else
            {
                return;
            }

            Dirty(ent);
            UpdateUi(ent);
            return;
        }

        // Канистра пополнения: весь её товар в автомат.
        if (TryComp<ImperialCustomVendorRefillComponent>(used, out var refill) && IsOwner(ent, user))
        {
            args.Handled = true;
            var loaded = 0;
            if (_container.TryGetContainer(used, refill.Container, out var canister))
            {
                foreach (var item in canister.ContainedEntities.ToList())
                {
                    if (Storage(ent).ContainedEntities.Count >= ent.Comp.MaxItems)
                        break;
                    if (_container.Insert(item, Storage(ent)))
                    {
                        ent.Comp.Prices.TryAdd(GetNetEntity(item), 0);
                        loaded++;
                    }
                }
            }

            Dirty(ent);
            UpdateUi(ent);
            _popup.PopupEntity(Loc.GetString(loaded > 0 ? "imperial-custom-vendor-restocked" : "imperial-custom-vendor-nothing", ("count", loaded)), ent, user);
            return;
        }

        if (!IsOwner(ent, user))
            return;

        args.Handled = true;
        if (HasComp<StackComponent>(used))
        {
            Speak(ent, Loc.GetString("imperial-custom-vendor-no-stacks"));
            return;
        }

        if (Storage(ent).ContainedEntities.Count >= ent.Comp.MaxItems)
        {
            Speak(ent, Loc.GetString("imperial-custom-vendor-full"));
            return;
        }

        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        // custom_price: цену задаёт владелец перед загрузкой.
        _quickDialog.OpenDialog(actor.PlayerSession, Loc.GetString("imperial-custom-vendor-price-title"), Loc.GetString("imperial-custom-vendor-price-prompt"), (int price) =>
        {
            if (Deleted(ent) || Deleted(used) || price < 0 || !_hands.IsHolding(user, used))
                return;

            if (!_container.Insert(used, Storage(ent)))
                return;

            ent.Comp.Prices[GetNetEntity(used)] = price;
            Dirty(ent);
            UpdateUi(ent);
            _popup.PopupEntity(Loc.GetString("imperial-custom-vendor-loaded", ("item", used), ("price", price)), ent, user);
        });
    }

    private void OnExamined(Entity<ImperialCustomVendorComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Account != null)
            args.PushMarkup(Loc.GetString("imperial-custom-vendor-examine-locked", ("owner", ent.Comp.Owner)));
        args.PushMarkup(Loc.GetString("imperial-custom-vendor-examine-count", ("count", Storage(ent).ContainedEntities.Count), ("max", ent.Comp.MaxItems)));
    }

    private void OnUiOpened(Entity<ImperialCustomVendorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<ImperialCustomVendorComponent> ent)
    {
        foreach (var actor in _ui.GetActors(ent.Owner, ImperialCustomVendorUiKey.Key))
        {
            var items = Storage(ent).ContainedEntities
                .Select(e => new ImperialCustomVendorEntry(GetNetEntity(e), MetaData(e).EntityPrototype?.ID ?? string.Empty, Name(e),
                    ent.Comp.Prices.GetValueOrDefault(GetNetEntity(e))))
                .ToList();
            var cash = (_bank.AccountOf(actor)?.Balance ?? 0) + _vending.CashOf(actor).Sum(e => Comp<StackComponent>(e).Count);
            _ui.SetUiState(ent.Owner, ImperialCustomVendorUiKey.Key, new ImperialCustomVendorState(ent.Comp.Owner, IsOwner(ent, actor), cash, items));
        }
    }

    /// <summary>Покупка: со счёта или наличными, деньги — владельцу. Владелец забирает бесплатно.</summary>
    private void OnBuy(Entity<ImperialCustomVendorComponent> ent, ref ImperialCustomVendorBuyMessage args)
    {
        var item = GetEntity(args.Item);
        var user = args.Actor;
        if (!Storage(ent).Contains(item))
            return;

        if (!IsOwner(ent, user))
        {
            if (ent.Comp.Account is not { } ownerId || _bank.Get(ownerId) is not { } owner)
            {
                Speak(ent, Loc.GetString("imperial-custom-vendor-no-owner"));
                return;
            }

            var price = ent.Comp.Prices.GetValueOrDefault(args.Item);
            if (price > 0)
            {
                var account = _bank.AccountOf(user);
                var paid = account != null && _bank.Adjust(account, -price, Loc.GetString("imperial-custom-vendor-reason", ("vendor", Name(ent))))
                    || _vending.PayCash(user, price);
                if (!paid)
                {
                    _popup.PopupEntity(Loc.GetString("imperial-custom-vendor-no-funds"), ent, user);
                    return;
                }

                _bank.Adjust(owner, price, Loc.GetString("imperial-custom-vendor-sale", ("item", Name(item))));
                _bank.CardTalk(owner, Loc.GetString("imperial-custom-vendor-sold", ("item", Name(item)), ("price", price)));
            }

            Speak(ent, Loc.GetString("imperial-custom-vendor-thanks"));
        }

        _container.Remove(item, Storage(ent));
        _hands.PickupOrDrop(user, item);
        _audio.PlayPvs(VendSound, ent);
        UpdateUi(ent);
    }

    private void OnPrice(Entity<ImperialCustomVendorComponent> ent, ref ImperialCustomVendorPriceMessage args)
    {
        if (!IsOwner(ent, args.Actor) || args.Price < 0 || !ent.Comp.Prices.ContainsKey(args.Item))
            return;

        ent.Comp.Prices[args.Item] = args.Price;
        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnRemoved(Entity<ImperialCustomVendorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Container)
            return;

        if (ent.Comp.Prices.Remove(GetNetEntity(args.Entity)))
            Dirty(ent);
    }

    /// <summary>on_deconstruction(): с привязанным счётом — самоуничтожение, иначе товар высыпается.</summary>
    private void OnDeconstructed(Entity<ImperialCustomVendorComponent> ent, ref MachineDeconstructedEvent args)
    {
        if (ent.Comp.Account != null)
        {
            foreach (var item in Storage(ent).ContainedEntities.ToList())
                QueueDel(item);
            _explosion.QueueExplosion(_transform.GetMapCoordinates(ent), "Default", 30, 3, 10, ent, 0);
            return;
        }

        _container.EmptyContainer(Storage(ent), destination: Transform(ent).Coordinates);
    }

    private void Speak(EntityUid ent, string message)
    {
        _chat.TrySendInGameICMessage(ent, message, InGameICChatType.Speak, false);
    }
}
