using System.Linq;
using Content.Server.Imperial.Medieval.Achievements;
using Content.Server.Actions;
using Content.Server.Administration.Logs;
using Content.Server.Quest.Components;
using Content.Server.Stack;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.Imperial.Medieval.Achievements;
using Content.Shared.Actions;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Mind;
using Content.Shared.PDA.Ringer;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed partial class TradingSystem
{
    [Dependency] private readonly IAdminLogManager _admin = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly ActionUpgradeSystem _actionUpgrade = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly AchievementSystem _achievements = default!;

    private void InitializeUi()
    {
        SubscribeLocalEvent<TradingComponent, TradingRequestUpdateInterfaceMessage>(OnRequestUpdate);
        SubscribeLocalEvent<TradingComponent, TradingBuyMessage>(OnBuyRequest);
        SubscribeLocalEvent<TradingComponent, TradingRequestWithdrawMessage>(OnRequestWithdraw);
    }

    public void ToggleUi(EntityUid user, EntityUid storeEnt, TradingComponent? component = null)
    {
        if (!Resolve(storeEnt, ref component))
            return;

        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        if (!_ui.TryToggleUi(storeEnt, TradingUiKey.Key, actor.PlayerSession))
            return;

        UpdateUserInterface(user, storeEnt, component);
    }

    public void CloseUi(EntityUid uid, TradingComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _ui.CloseUi(uid, TradingUiKey.Key);
    }

    public void UpdateUserInterface(EntityUid? user, EntityUid store, TradingComponent? component = null)
    {
        if (!Resolve(store, ref component))
            return;

        if (user != null)
        {
            component.LastAvailableGuilds = GetAvailableGuilds(component.AccountOwner ?? user.Value, store, component)
                .ToHashSet();
        }

        var netUser = GetNetEntity(component.AccountOwner ?? user);

        var state = new TradingUpdateState(component.LastAvailableGuilds, component.Balance, component.Currency, netUser);
        _ui.SetUiState(store, TradingUiKey.Key, state);
    }

    private void OnRequestUpdate(EntityUid uid, TradingComponent component, TradingRequestUpdateInterfaceMessage args)
    {
        UpdateUserInterface(args.Actor, GetEntity(args.Entity), component);
    }

    private void BeforeActivatableUiOpen(EntityUid uid, TradingComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateUserInterface(args.User, uid, component);
    }

    private void OnBuyRequest(EntityUid uid, TradingComponent component, TradingBuyMessage msg)
    {
        var item = component.Guilds
            .Where(x => x.Id == msg.Item.GuildId)
            .SelectMany(x => x.Items)
            .FirstOrDefault(item => item.Equals(msg.Item));

        if (item == null)
        {
            Log.Debug("item does not exist");
            return;
        }

        var buyer = msg.Actor;
        var netBuyer = GetNetEntity(buyer);

        var guild = component.Guilds.FirstOrDefault(g => g.Id == item.GuildId);
        if (guild == null)
            return;

        if (!item.CanBuy(netBuyer, guild, _entityManager).Item1)
            return;

        var cost = TradingHelpers.PriceWithReputation(guild, item, netBuyer);
        if (component.Balance < cost)
            return;

        component.Balance -= cost;

        string? name = null;
        if (TryComp(buyer, out MetaDataComponent? meta))
            name = meta.EntityName;

        guild?.AddReputation(netBuyer, item.ReputationForBuying, name);

        if (HasComp<AchievementOwnerComponent>(buyer))
        {
            _achievements.TryUpdateProgressAndGrant(
                buyer,
                new GuildReputationContext(guild!.TypePrototype, guild!.GetReputation(netBuyer)),
                ach => ach.Conditions.Any(c => c is GuildReputationCondition)
            );
        }

        if (item.ProductEntity != null)
        {
            var product = Spawn(item.ProductEntity, Transform(buyer).Coordinates);

            // TODO: переделать это и желательно систему квестов
            if (TryComp<PalletContractComponent>(product, out var pallet))
                pallet.ContractGuildId = guild?.Id;
            if (TryComp<QuestContractComponent>(product, out var quest))
                quest.ContractGuildId = guild?.Id;

            _hands.PickupOrDrop(buyer, product);

            var xForm = Transform(product);
        }

        _admin.Add(LogType.StorePurchase,
            LogImpact.Low,
            $"{ToPrettyString(buyer):player} medieval purchased item \"{TradingLocalisationHelpers.GetLocalisedNameOrEntityName(item, _prototypeManager)}\" from {ToPrettyString(uid)}");

        _audio.PlayEntity(component.BuySuccessSound, msg.Actor, uid);
        UpdateUserInterface(buyer, uid, component);
    }

    private void OnRequestWithdraw(EntityUid uid, TradingComponent component, TradingRequestWithdrawMessage msg)
    {
        if (msg.Amount <= 0)
            return;

        if (component.Balance < msg.Amount)
            return;

        if (!_prototypeManager.TryIndex(component.Currency, out var proto))
            return;

        if (proto.Cash == null || !proto.CanWithdraw)
            return;

        var buyer = msg.Actor;

        FixedPoint2 amountRemaining = msg.Amount;
        var coordinates = Transform(buyer).Coordinates;

        var sortedCashValues = proto.Cash.Keys.OrderByDescending(x => x).ToList();
        foreach (var value in sortedCashValues)
        {
            var cashId = proto.Cash[value];
            var amountToSpawn = (int) MathF.Floor((float) (amountRemaining / value));
            var ents = _stack.SpawnMultiple(cashId, amountToSpawn, coordinates);
            if (ents.FirstOrDefault() is {} ent)
                _hands.PickupOrDrop(buyer, ent);
            amountRemaining -= value * amountToSpawn;
        }

        component.Balance -= msg.Amount;
        UpdateUserInterface(buyer, uid, component);
    }
}
