using System.Numerics;
using Content.Client.Imperial.Fishing;
using Content.Shared.Imperial.Cargo;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cargo;

/// <summary>Окно экспресс-консоли (tgui CargoExpress + CargoCatalog express).</summary>
public sealed class ImperialExpressConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private Control _lockedView = default!;
    private Control _mainView = default!;
    private PanelContainer _notice = default!;
    private Label _noticeText = default!;
    private Label _points = default!;
    private Button _cargoBay = default!;
    private Button _beacon = default!;
    private Button _print = default!;
    private ImperialCargoCatalog _catalog = default!;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-express-ui-title");
        _window.SetSize = new Vector2(600, 700);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };

        var locked = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        locked.AddChild(new Label
        {
            Text = Loc.GetString("imperial-express-ui-locked"),
            FontColorOverride = Color.FromHex("#db2828"),
            HorizontalAlignment = Control.HAlignment.Center,
        });
        _lockedView = ImperialFishingUi.Section(null, locked);
        _lockedView.VerticalExpand = true;
        root.AddChild(_lockedView);

        var main = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        _mainView = main;

        _notice = new PanelContainer { Margin = new Thickness(0, 0, 0, 4) };
        _noticeText = new Label { Margin = new Thickness(6, 3) };
        _notice.AddChild(_noticeText);
        main.AddChild(_notice);

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var title = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        title.AddChild(new Label { Text = Loc.GetString("imperial-express-ui-section"), StyleClasses = { "LabelHeading" }, HorizontalExpand = true });
        _points = new Label();
        title.AddChild(_points);
        header.AddChild(title);

        var landing = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        landing.AddChild(new Label { Text = Loc.GetString("imperial-express-ui-landing"), MinWidth = 150 });
        _cargoBay = new Button { Text = Loc.GetString("imperial-express-ui-cargo-bay"), ToggleMode = true };
        _cargoBay.OnPressed += _ => SendMessage(new ImperialExpressActionMessage(ImperialExpressAction.LandingCargo));
        landing.AddChild(_cargoBay);
        _beacon = new Button { ToggleMode = true, ClipText = true, MaxWidth = 200 };
        _beacon.OnPressed += _ => SendMessage(new ImperialExpressActionMessage(ImperialExpressAction.LandingBeacon));
        landing.AddChild(_beacon);
        _print = new Button();
        _print.OnPressed += _ => SendMessage(new ImperialExpressActionMessage(ImperialExpressAction.PrintBeacon));
        landing.AddChild(_print);
        header.AddChild(landing);
        main.AddChild(ImperialFishingUi.Section(null, header));

        _catalog = new ImperialCargoCatalog();
        _catalog.OnBuy += id => SendMessage(new ImperialExpressBuyMessage(id));
        main.AddChild(_catalog);

        root.AddChild(main);
        _window.Contents.AddChild(root);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialExpressConsoleState express || _window == null)
            return;

        _lockedView.Visible = express.Locked;
        _mainView.Visible = !express.Locked;

        _notice.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex(express.CanBeacon ? "#2a5b9e" : "#9e2a2a") };
        _noticeText.Text = express.Message;
        _points.Text = Loc.GetString("imperial-express-ui-points", ("points", express.Points));
        _cargoBay.Pressed = !express.UsingBeacon;
        _beacon.Text = express.BeaconName;
        _beacon.Pressed = express.UsingBeacon;
        _beacon.Disabled = !express.HasBeacon;
        _print.Text = express.PrintMessage;
        _print.Disabled = !express.CanBuyBeacon;
        _catalog.Update(express.Packs, express.Points);
    }
}

/// <summary>Личный заказ на консоли карго (Buy Privately).</summary>
public sealed class ImperialPrivateOrderBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private Label _holder = default!;
    private Label _message = default!;
    private ImperialCargoCatalog _catalog = default!;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-private-order-title");
        _window.SetSize = new Vector2(600, 650);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _holder = new Label { StyleClasses = { "LabelHeading" } };
        _message = new Label();
        header.AddChild(_holder);
        header.AddChild(_message);
        root.AddChild(ImperialFishingUi.Section(null, header));

        _catalog = new ImperialCargoCatalog();
        _catalog.OnBuy += id => SendMessage(new ImperialPrivateOrderBuyMessage(id));
        root.AddChild(_catalog);
        _window.Contents.AddChild(root);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialPrivateOrderState order || _window == null)
            return;

        _holder.Text = order.Holder == string.Empty
            ? Loc.GetString("imperial-private-order-no-card")
            : Loc.GetString("imperial-private-order-holder", ("holder", order.Holder), ("balance", order.Balance));
        _message.Text = order.Message;
        _catalog.Update(order.Packs, order.Balance);
    }
}
