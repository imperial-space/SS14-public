using System.Numerics;
using Content.Client.Imperial.Fishing;
using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.Imperial.Cargo;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cargo;

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
