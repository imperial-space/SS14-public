using System.Numerics;
using Content.Shared.Imperial.Uplink.Stealth;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Uplink;

/// <summary>launchpad_remote TG: статус, смещение X/Y и кнопки «Отправить» / «Забрать».</summary>
public sealed class UplinkLaunchpadBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private Label _status = default!;
    private SpinBox _x = default!;
    private SpinBox _y = default!;
    private Button _send = default!;
    private Button _pull = default!;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("uplink-launchpad-title");
        _window.SetSize = new Vector2(340, 230);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(6) };
        _status = new Label();
        root.AddChild(_status);
        root.AddChild(new Label { Text = Loc.GetString("uplink-launchpad-ui-offset"), Margin = new Thickness(0, 6, 0, 2) });

        _x = new SpinBox { HorizontalExpand = true };
        _y = new SpinBox { HorizontalExpand = true };
        foreach (var (label, box) in new[] { ("X", _x), ("Y", _y) })
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            row.AddChild(new Label { Text = label, MinWidth = 24 });
            box.InitDefaultButtons();
            box.ValueChanged += _ => SendMessage(new UplinkLaunchpadOffsetMessage(_x.Value, _y.Value));
            row.AddChild(box);
            root.AddChild(row);
        }

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        _send = new Button { Text = Loc.GetString("uplink-launchpad-send"), HorizontalExpand = true };
        _send.OnPressed += _ => SendMessage(new UplinkLaunchpadFireMessage(true));
        _pull = new Button { Text = Loc.GetString("uplink-launchpad-retrieve"), HorizontalExpand = true };
        _pull.OnPressed += _ => SendMessage(new UplinkLaunchpadFireMessage(false));
        buttons.AddChild(_send);
        buttons.AddChild(_pull);
        root.AddChild(buttons);
        _window.Contents.AddChild(root);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not UplinkLaunchpadState s)
            return;

        _status.Text = Loc.GetString(!s.Linked ? "uplink-launchpad-ui-unlinked" : s.Deployed ? "uplink-launchpad-ui-ready" : "uplink-launchpad-ui-folded");
        _x.IsValid = v => Math.Abs(v) <= s.Range;
        _y.IsValid = v => Math.Abs(v) <= s.Range;
        _x.OverrideValue(s.X);
        _y.OverrideValue(s.Y);
        _send.Disabled = _pull.Disabled = !s.Deployed;
    }
}
