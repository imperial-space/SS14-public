using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.Imperial.Cadia;

/// <summary>Окно огневого поста: голокарта поверхности слева, состояние батареи и кнопка огня справа.</summary>
public sealed class CadiaBatteryWindow : DefaultWindow
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public event Action<Vector2>? OnTarget;
    public event Action? OnFire;

    private readonly CadiaHolomapControl _map = new();
    private readonly Label _status = new() { FontColorOverride = Color.FromHex("#e8c66a") };
    private readonly Label _ammo = new();
    private readonly Label _reload = new();
    private readonly Label _target = new();
    private readonly RichTextLabel _hint = new();
    private readonly ProgressBar _reloadBar = new() { MinHeight = 14, MinValue = 0, MaxValue = 1 };
    private readonly Button _fire = new() { MinHeight = 48 };

    private CadiaBatteryBuiState? _state;
    private TimeSpan _reloadTotal = TimeSpan.FromSeconds(45);

    public CadiaBatteryWindow()
    {
        IoCManager.InjectDependencies(this);
        MinSize = SetSize = new Vector2(860, 600);

        _map.HorizontalExpand = true;
        _map.VerticalExpand = true;
        _map.OnClicked += pos => OnTarget?.Invoke(pos);

        _fire.Text = Loc.GetString("cadia-battery-ui-fire");
        _fire.StyleClasses.Add("Caution");
        _fire.OnPressed += _ => OnFire?.Invoke();
        _hint.SetMessage(FormattedMessage.FromMarkupOrThrow(Loc.GetString("cadia-battery-ui-hint")), defaultColor: Color.FromHex("#9a9a8a"));

        var side = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 260,
            MaxWidth = 260,
            SeparationOverride = 8,
            Margin = new Thickness(8, 0, 0, 0),
        };
        side.AddChild(_status);
        side.AddChild(_ammo);
        side.AddChild(_reload);
        side.AddChild(_reloadBar);
        side.AddChild(_target);
        side.AddChild(new Control { VerticalExpand = true });
        side.AddChild(Legend());
        side.AddChild(_hint);
        side.AddChild(_fire);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(6) };
        var frame = new PanelContainer
        {
            MinSize = new Vector2(440, 440),
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#050a06"), BorderColor = Color.FromHex("#3d6b45"), BorderThickness = new Thickness(2) },
        };
        frame.AddChild(_map);
        root.AddChild(frame);
        root.AddChild(side);
        Contents.AddChild(root);
    }

    private Control Legend()
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        foreach (var (color, key) in new[]
                 {
                     (CadiaHolomapControl.FriendlyColor, "cadia-battery-ui-legend-friendly"),
                     (CadiaHolomapControl.PylonColor, "cadia-battery-ui-legend-pylon"),
                     (CadiaHolomapControl.CitadelColor, "cadia-battery-ui-legend-citadel"),
                     (CadiaHolomapControl.StrikeColor, "cadia-battery-ui-legend-strike"),
                     (CadiaHolomapControl.ShipColor, "cadia-battery-ui-legend-ship"),
                 })
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            row.AddChild(new PanelContainer { MinSize = new Vector2(10, 10), VerticalAlignment = VAlignment.Center, PanelOverride = new StyleBoxFlat { BackgroundColor = color } });
            row.AddChild(new Label { Text = Loc.GetString(key), FontColorOverride = Color.FromHex("#b8c8b8") });
            box.AddChild(row);
        }
        return box;
    }

    public void Update(CadiaBatteryBuiState state)
    {
        if (_state == null || state.NextShot != _state.NextShot)
            _reloadTotal = state.NextShot - _timing.CurTime > _reloadTotal ? state.NextShot - _timing.CurTime : _reloadTotal;
        _state = state;
        _map.SetState(state);

        _status.Text = state.InOrbit
            ? Loc.GetString(state.Kind == CadiaBatteryKind.Lance ? "cadia-battery-ui-status-lance" : "cadia-battery-ui-status-macro")
            : state.Denial ?? string.Empty;
        _status.FontColorOverride = state.InOrbit ? Color.FromHex("#e8c66a") : Color.FromHex("#ff5a4a");

        _ammo.Visible = state.Kind == CadiaBatteryKind.Macro;
        _ammo.Text = Loc.GetString("cadia-battery-ui-ammo", ("ammo", state.Ammo), ("max", state.MaxAmmo));
        _target.Text = state.Target is { } t
            ? Loc.GetString("cadia-battery-ui-target", ("x", (int)t.X), ("y", (int)t.Y))
            : Loc.GetString("cadia-battery-ui-no-target");
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_state == null)
            return;

        var left = _state.NextShot - _timing.CurTime;
        var ready = left <= TimeSpan.Zero;
        _reload.Text = ready
            ? Loc.GetString("cadia-battery-ui-ready")
            : Loc.GetString("cadia-battery-ui-reloading", ("seconds", (int)Math.Ceiling(left.TotalSeconds)));
        _reload.FontColorOverride = ready ? Color.FromHex("#6dff8a") : Color.FromHex("#ff9a3c");
        _reloadBar.Value = ready ? 1f : 1f - (float)(left.TotalSeconds / Math.Max(1, _reloadTotal.TotalSeconds));
        _fire.Disabled = !_state.InOrbit || !ready || _state.Target == null ||
                         (_state.Kind == CadiaBatteryKind.Macro && _state.Ammo <= 0);
    }
}
