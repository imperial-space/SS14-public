using System.Numerics;
using Content.Client.Imperial.Fishing;
using Content.Shared.Imperial.Cargo;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cargo;

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
