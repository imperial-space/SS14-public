using System.Linq;
using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.Imperial.Cargo;
using Content.Shared.Interaction;
using Content.Shared.PDA;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Cargo;

/// <summary>
/// Доступ к личному счёту без действий с ID-картой: приложение NT Pay в КПК (баланс, история, переводы)
/// и банкомат (снятие и внесение наличных).
/// </summary>
public sealed class ImperialBankAppsSystem : EntitySystem
{
    [Dependency] private readonly ImperialBankSystem _bank = default!;
    [Dependency] private readonly CartridgeLoaderSystem _loader = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    /// <summary>Сколько последних операций показывать.</summary>
    private const int HistoryLength = 20;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialNtPayCartridgeComponent, CartridgeUiReadyEvent>(OnNtPayReady);
        SubscribeLocalEvent<ImperialNtPayCartridgeComponent, CartridgeMessageEvent>(OnNtPayMessage);

        SubscribeLocalEvent<ImperialAtmComponent, BoundUIOpenedEvent>(OnAtmOpened);
        SubscribeLocalEvent<ImperialAtmComponent, ImperialAtmWithdrawMessage>(OnAtmWithdraw);
        SubscribeLocalEvent<ImperialAtmComponent, InteractUsingEvent>(OnAtmInteractUsing);
    }

    #region NT Pay

    /// <summary>Счёт приложения, а если его нет — счёт ID-карты в КПК.</summary>
    private ImperialBankSystem.Account? AppAccount(EntityUid program, EntityUid loader)
    {
        if (TryComp<ImperialNtPayCartridgeComponent>(program, out var app) && _bank.Get(app.AccountId) is { } account)
            return account;

        if (TryComp<PdaComponent>(loader, out var pda) && pda.ContainedId is { } id
            && TryComp<ImperialBankCardComponent>(id, out var card))
            return _bank.Get(card.AccountId);

        return null;
    }

    private void OnNtPayReady(EntityUid ent, ImperialNtPayCartridgeComponent comp, CartridgeUiReadyEvent args)
    {
        UpdateNtPay(ent, args.Loader);
    }

    private void OnNtPayMessage(EntityUid ent, ImperialNtPayCartridgeComponent comp, CartridgeMessageEvent args)
    {
        if (args is not ImperialNtPayTransferMessage transfer)
            return;

        var loader = GetEntity(args.LoaderUid);
        string message;
        if (AppAccount(ent, loader) is not { } from)
            message = Loc.GetString("imperial-ntpay-no-account");
        else if (_bank.Get(transfer.Target) is not { } to)
            message = Loc.GetString("imperial-ntpay-no-target", ("id", transfer.Target));
        else if (to == from)
            message = Loc.GetString("imperial-ntpay-self");
        else if (!_bank.Transfer(from, to, transfer.Amount))
            message = Loc.GetString("imperial-ntpay-no-money");
        else
            message = Loc.GetString("imperial-ntpay-sent", ("amount", transfer.Amount), ("holder", to.Holder));

        UpdateNtPay(ent, loader, message);
    }

    private void UpdateNtPay(EntityUid program, EntityUid loader, string? message = null)
    {
        var account = AppAccount(program, loader);
        var history = account == null
            ? new List<string>()
            : account.History.AsEnumerable().Reverse().Take(HistoryLength)
                .Select(h => $"{h.Time:hh\\:mm}  {(h.Amount >= 0 ? "+" : string.Empty)}{h.Amount}  {h.Reason}")
                .ToList();

        var state = new ImperialNtPayUiState(account?.Holder ?? string.Empty, account?.Id ?? 0, account?.Balance ?? 0,
            account?.Debt ?? 0, history, message ?? string.Empty);
        _loader.UpdateCartridgeUiState(loader, state);
    }

    #endregion

    #region Банкомат

    private void OnAtmOpened(Entity<ImperialAtmComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateAtm(ent, args.Actor);
    }

    private void UpdateAtm(EntityUid atm, EntityUid user, string? message = null)
    {
        var account = _bank.AccountOf(user);
        message ??= Loc.GetString(account == null ? "imperial-atm-no-account" : "imperial-atm-hint");
        _ui.SetUiState(atm, ImperialAtmUiKey.Key,
            new ImperialAtmState(account?.Holder ?? string.Empty, account?.Id ?? 0, account?.Balance ?? 0, message));
    }

    private void OnAtmWithdraw(Entity<ImperialAtmComponent> ent, ref ImperialAtmWithdrawMessage args)
    {
        if (_bank.AccountOf(args.Actor) is not { } account)
        {
            UpdateAtm(ent, args.Actor);
            return;
        }

        var ok = _bank.Withdraw(account, args.Actor, args.Amount);
        UpdateAtm(ent, args.Actor, Loc.GetString(ok ? "imperial-atm-withdrawn" : "imperial-atm-no-money", ("amount", args.Amount)));
    }

    private void OnAtmInteractUsing(Entity<ImperialAtmComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || _bank.AccountOf(args.User) is not { } account)
            return;

        if (_bank.Deposit(account, args.Used))
        {
            args.Handled = true;
            UpdateAtm(ent, args.User);
        }
    }

    #endregion
}
