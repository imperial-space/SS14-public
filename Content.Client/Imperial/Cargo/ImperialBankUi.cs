using System.Numerics;
using Content.Client.Imperial.Fishing;
using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.Imperial.Cargo;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cargo;

/// <summary>Приложение NT Pay в КПК: счёт, баланс, перевод по номеру счёта и история операций.</summary>
public sealed partial class ImperialNtPayUi : UIFragment
{
    private BoxContainer? _root;
    private Label _holder = default!;
    private Label _balance = default!;
    private Label _message = default!;
    private LineEdit _target = default!;
    private LineEdit _amount = default!;
    private BoxContainer _history = default!;

    public override Control GetUIFragmentRoot()
    {
        return _root!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true, Margin = new Thickness(4) };

        var info = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _holder = new Label { StyleClasses = { "LabelHeading" } };
        _balance = new Label();
        info.AddChild(_holder);
        info.AddChild(_balance);
        _root.AddChild(ImperialFishingUi.Section(Loc.GetString("imperial-ntpay-title"), info));

        var transfer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        _target = new LineEdit { PlaceHolder = Loc.GetString("imperial-ntpay-target"), HorizontalExpand = true };
        _amount = new LineEdit { PlaceHolder = Loc.GetString("imperial-ntpay-amount"), MinWidth = 80 };
        var send = new Button { Text = Loc.GetString("imperial-ntpay-send") };
        send.OnPressed += _ =>
        {
            if (!int.TryParse(_target.Text, out var target) || !int.TryParse(_amount.Text, out var amount))
                return;
            userInterface.SendMessage(new CartridgeUiMessage(new ImperialNtPayTransferMessage(target, amount)));
            _amount.Text = string.Empty;
        };
        row.AddChild(_target);
        row.AddChild(_amount);
        row.AddChild(send);
        transfer.AddChild(row);
        _message = new Label { FontColorOverride = Color.FromHex("#d09000") };
        transfer.AddChild(_message);
        _root.AddChild(ImperialFishingUi.Section(Loc.GetString("imperial-ntpay-transfer"), transfer));

        _history = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_history);
        var historySection = ImperialFishingUi.Section(Loc.GetString("imperial-ntpay-history"), scroll);
        historySection.VerticalExpand = true;
        _root.AddChild(historySection);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not ImperialNtPayUiState pay || _root == null)
            return;

        _holder.Text = pay.AccountId == 0
            ? Loc.GetString("imperial-ntpay-no-account")
            : Loc.GetString("imperial-ntpay-holder", ("holder", pay.Holder), ("id", pay.AccountId));
        _balance.Text = pay.Debt > 0
            ? Loc.GetString("imperial-ntpay-balance-debt", ("balance", pay.Balance), ("debt", pay.Debt))
            : Loc.GetString("imperial-ntpay-balance", ("balance", pay.Balance));
        _message.Text = pay.Message;

        _history.RemoveAllChildren();
        foreach (var line in pay.History)
        {
            _history.AddChild(new Label { Text = line, ClipText = true });
        }
    }
}

/// <summary>Окно банкомата: снятие наличных; внести — вставить кредиты в банкомат.</summary>
public sealed class ImperialAtmBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private Label _holder = default!;
    private Label _message = default!;
    private LineEdit _amount = default!;
    private Button _withdraw = default!;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-atm-title");
        _window.SetSize = new Vector2(360, 200);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _holder = new Label { StyleClasses = { "LabelHeading" } };
        root.AddChild(_holder);

        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 6) };
        _amount = new LineEdit { PlaceHolder = Loc.GetString("imperial-ntpay-amount"), HorizontalExpand = true };
        _withdraw = new Button { Text = Loc.GetString("imperial-atm-withdraw") };
        _withdraw.OnPressed += _ =>
        {
            if (int.TryParse(_amount.Text, out var amount) && amount > 0)
                SendMessage(new ImperialAtmWithdrawMessage(amount));
        };
        row.AddChild(_amount);
        row.AddChild(_withdraw);
        root.AddChild(row);

        _message = new Label();
        root.AddChild(_message);
        _window.Contents.AddChild(ImperialFishingUi.Section(null, root));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialAtmState atm || _window == null)
            return;

        _holder.Text = atm.AccountId == 0
            ? Loc.GetString("imperial-ntpay-no-account")
            : Loc.GetString("imperial-atm-holder", ("holder", atm.Holder), ("id", atm.AccountId), ("balance", atm.Balance));
        _withdraw.Disabled = atm.AccountId == 0;
        _message.Text = atm.Message;
    }
}
