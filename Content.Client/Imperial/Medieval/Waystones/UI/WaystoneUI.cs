using System.Numerics;
using Content.Shared.Imperial.Medieval.Waystones;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Content.Client.Imperial.Medieval.Waystones.UI;

namespace Content.Client.Imperial.Medieval.Waystones;

public sealed class WaystoneListWindow : DefaultWindow
{
    private readonly BoxContainer _container;
    public Action<NetEntity>? OnItemSelected;

    public WaystoneListWindow()
    {
        Title = $"{Loc.GetString("waystone-xaml-network")}";
        SetSize = new Vector2(600, 500);

        var scroll = new ScrollContainer { VerticalExpand = true };
        _container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
        };

        scroll.AddChild(_container);
        Contents.AddChild(scroll);
    }

    public void PopulateList(List<WaystoneInfo> waystones)
    {
        _container.DisposeAllChildren();
        foreach (var wp in waystones)
        {
            var button = new Button
            {
                Text = $"{wp.Name}. Цена: {wp.DeparturePrice + wp.ArrivalPrice} ({wp.DeparturePrice}  +  {wp.ArrivalPrice})",
                HorizontalExpand = true
            };
            button.OnPressed += _ => OnItemSelected?.Invoke(wp.Entity);
            _container.AddChild(button);
        }
    }
}

public sealed class WaystoneBoundUserInterface : BoundUserInterface
{
    private WaystoneListWindow? _window;

    public WaystoneBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = new WaystoneListWindow();
        _window.OnClose += Close;
        _window.OnItemSelected += ent => SendMessage(new WaystoneSelectMessage(ent));
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is WaystoneUpdateState msg)
            _window?.PopulateList(msg.Waystones);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _window?.Dispose();
    }
}


public sealed class WaystoneBoundUserInterfaceAdmin : BoundUserInterface
{
    private WaystoneAdminMenu? _menu;

    public WaystoneBoundUserInterfaceAdmin(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _menu = new WaystoneAdminMenu();
        _menu.OnClose += Close;

        _menu.OnApplyPressed += (departurePrice, arrivalPrice, state) =>
        {
            SendMessage(new WaystoneStateMessage(departurePrice, arrivalPrice, state));
        };

        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is WaystoneUpdateState msg && _menu != null)
        {
            foreach (var waystone in msg.Waystones)
            {
                if (EntMan.GetEntity(waystone.Entity) == Owner)
                {
                    _menu.UpdateValues(waystone.DeparturePrice, waystone.ArrivalPrice, waystone.IsEnable);
                    break;
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _menu?.Close();
        _menu = null;
    }
}
