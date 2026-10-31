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

public sealed class CadiaBatteryBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CadiaBatteryWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CadiaBatteryWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnTarget += target => SendMessage(new CadiaBatterySetTargetMessage(target));
        _window.OnFire += () => SendMessage(new CadiaBatteryFireMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CadiaBatteryBuiState battery)
            _window?.Update(battery);
    }
}

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
            ? Loc.GetString("cadia-battery-ui-target", ("x", (int) t.X), ("y", (int) t.Y))
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
            : Loc.GetString("cadia-battery-ui-reloading", ("seconds", (int) Math.Ceiling(left.TotalSeconds)));
        _reload.FontColorOverride = ready ? Color.FromHex("#6dff8a") : Color.FromHex("#ff9a3c");
        _reloadBar.Value = ready ? 1f : 1f - (float) (left.TotalSeconds / Math.Max(1, _reloadTotal.TotalSeconds));
        _fire.Disabled = !_state.InOrbit || !ready || _state.Target == null ||
                         (_state.Kind == CadiaBatteryKind.Macro && _state.Ammo <= 0);
    }
}

/// <summary>
/// Голокарта поверхности: плитки окна под кораблём рисуются одной текстурой (зелёная развёртка ауспекса),
/// поверх — отметки, радиус досягаемости, перекрестие цели и круг разброса. Клик ставит цель.
/// </summary>
public sealed class CadiaHolomapControl : Control
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public static readonly Color FriendlyColor = Color.FromHex("#5aff78");
    public static readonly Color PylonColor = Color.FromHex("#3dffc8");
    public static readonly Color CitadelColor = Color.FromHex("#ffd37a");
    public static readonly Color StrikeColor = Color.FromHex("#ff3a2a");
    public static readonly Color ShipColor = Color.FromHex("#8fd8ff");
    private static readonly Color RangeColor = Color.FromHex("#3d6b4580");
    private static readonly Color TargetColor = Color.FromHex("#ff3a2a");

    public event Action<Vector2>? OnClicked;

    private CadiaBatteryBuiState? _state;
    private Texture? _texture;
    private readonly Font _font;

    public CadiaHolomapControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 13);
    }

    public void SetState(CadiaBatteryBuiState state)
    {
        var rebuild = _state == null || _state.Size != state.Size || _state.Origin != state.Origin || !_state.Tiles.AsSpan().SequenceEqual(state.Tiles);
        _state = state;
        if (rebuild)
            BuildTexture();
    }

    private void BuildTexture()
    {
        _texture = null;
        if (_state == null || _state.Size == 0 || _state.Tiles.Length != _state.Size * _state.Size)
            return;

        var size = _state.Size;
        using var image = new Image<Rgba32>(size, size);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                // Текстура сверху вниз, мир — снизу вверх.
                var tile = _state.Tiles[y * size + x];
                image[x, size - 1 - y] = tile switch
                {
                    1 => new Rgba32(22, 58, 30, 255),
                    2 => new Rgba32(120, 220, 140, 255),
                    _ => new Rgba32(4, 10, 6, 255),
                };
            }
        }
        _texture = _clyde.LoadTextureFromImage(image);
    }

    /// <summary>Сколько пикселей экрана на тайл и левый нижний угол окна карты на экране.</summary>
    private (float Scale, Vector2 Offset) Layout()
    {
        if (_state == null || _state.Size == 0)
            return (1, Vector2.Zero);
        var scale = MathF.Min(PixelSize.X, PixelSize.Y) / _state.Size;
        var offset = new Vector2((PixelSize.X - scale * _state.Size) / 2f, (PixelSize.Y - scale * _state.Size) / 2f);
        return (scale, offset);
    }

    private Vector2 ToScreen(Vector2 world)
    {
        var (scale, offset) = Layout();
        var local = world - (Vector2) _state!.Origin;
        return new Vector2(offset.X + local.X * scale, offset.Y + (_state.Size - local.Y) * scale);
    }

    private Vector2 ToWorld(Vector2 screen)
    {
        var (scale, offset) = Layout();
        return new Vector2((screen.X - offset.X) / scale + _state!.Origin.X, _state.Size - (screen.Y - offset.Y) / scale + _state.Origin.Y);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick || _state is not { InOrbit: true })
            return;
        OnClicked?.Invoke(ToWorld(args.RelativePixelPosition));
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_state == null || !_state.InOrbit || _texture == null)
        {
            var text = _state?.Denial ?? Loc.GetString("cadia-battery-ui-no-signal");
            var dims = handle.GetDimensions(_font, text, 1f);
            handle.DrawString(_font, (PixelSize - dims) / 2f, text, StrikeColor);
            return;
        }

        var (scale, offset) = Layout();
        var mapBox = UIBox2.FromDimensions(offset, new Vector2(_state.Size * scale, _state.Size * scale));
        handle.DrawTextureRect(_texture, mapBox);

        // Сетка голопроектора каждые 10 тайлов.
        var grid = Color.FromHex("#1e3a2440");
        for (var i = 0; i <= _state.Size; i += 10)
        {
            handle.DrawLine(new Vector2(offset.X + i * scale, mapBox.Top), new Vector2(offset.X + i * scale, mapBox.Bottom), grid);
            handle.DrawLine(new Vector2(mapBox.Left, offset.Y + i * scale), new Vector2(mapBox.Right, offset.Y + i * scale), grid);
        }

        // Бегущая строка развёртки.
        var sweep = (float) (_timing.RealTime.TotalSeconds % 3.0 / 3.0);
        var sy = mapBox.Top + mapBox.Height * sweep;
        handle.DrawRect(new UIBox2(mapBox.Left, sy - 2, mapBox.Right, sy + 2), Color.FromHex("#5aff7826"));

        handle.DrawCircle(ToScreen(_state.ShipPosition), _state.Range * scale, RangeColor, false);

        var pulse = 0.6f + 0.4f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 6f);
        foreach (var marker in _state.Markers)
        {
            var p = ToScreen(marker.Position);
            switch (marker.Kind)
            {
                case CadiaHoloMarkerKind.Friendly:
                    handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(2), new Vector2(4)), FriendlyColor);
                    break;
                case CadiaHoloMarkerKind.Pylon:
                    handle.DrawCircle(p, 5, PylonColor);
                    handle.DrawCircle(p, 8, PylonColor.WithAlpha(0.5f), false);
                    break;
                case CadiaHoloMarkerKind.Citadel:
                    handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(6), new Vector2(12)), CitadelColor, false);
                    handle.DrawCircle(p, 3, CitadelColor);
                    break;
                case CadiaHoloMarkerKind.Strike:
                    handle.DrawCircle(p, 3f * scale, StrikeColor.WithAlpha(0.35f * pulse));
                    handle.DrawCircle(p, 3f * scale, StrikeColor, false);
                    break;
                case CadiaHoloMarkerKind.Shuttle:
                    handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(5), new Vector2(10)), ShipColor.WithAlpha(0.6f), false);
                    break;
                case CadiaHoloMarkerKind.Designated:
                    // Цель со стола командования: жёлтый ромб с пульсом.
                    var d = 7f + 2f * pulse;
                    handle.DrawLine(p + new Vector2(0, -d), p + new Vector2(d, 0), CitadelColor);
                    handle.DrawLine(p + new Vector2(d, 0), p + new Vector2(0, d), CitadelColor);
                    handle.DrawLine(p + new Vector2(0, d), p + new Vector2(-d, 0), CitadelColor);
                    handle.DrawLine(p + new Vector2(-d, 0), p + new Vector2(0, -d), CitadelColor);
                    break;
                case CadiaHoloMarkerKind.Ship:
                    handle.DrawLine(p - new Vector2(9, 0), p + new Vector2(9, 0), ShipColor);
                    handle.DrawLine(p - new Vector2(0, 9), p + new Vector2(0, 9), ShipColor);
                    handle.DrawCircle(p, 4, ShipColor, false);
                    break;
            }
        }

        if (_state.Target is { } target)
        {
            var p = ToScreen(target);
            var r = Math.Max(_state.Spread * scale, 6f);
            handle.DrawCircle(p, r, TargetColor.WithAlpha(0.18f * pulse));
            handle.DrawCircle(p, r, TargetColor, false);
            handle.DrawLine(p - new Vector2(r + 6, 0), p + new Vector2(r + 6, 0), TargetColor);
            handle.DrawLine(p - new Vector2(0, r + 6), p + new Vector2(0, r + 6), TargetColor);
        }

        // Координаты под курсором.
        if (UserInterfaceManager.CurrentlyHovered == this)
        {
            var mouse = UserInterfaceManager.MousePositionScaled.Position * UIScale - GlobalPixelPosition;
            if (mapBox.Contains(mouse))
            {
                var world = ToWorld(mouse);
                handle.DrawString(_font, new Vector2(mapBox.Left + 6, mapBox.Top + 4), $"{(int) world.X}, {(int) world.Y}", FriendlyColor);
            }
        }
    }
}
