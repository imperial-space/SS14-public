using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

/// <summary>Башня зенитной позиции плавно поворачивается к углу, который присылает сервер.</summary>
public sealed class CadiaAntiAirVisualSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string TurretLayer = "turret";
    private readonly Dictionary<EntityUid, Angle> _shown = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<CadiaAntiAirComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var aa, out var sprite))
        {
            var shown = _shown.GetValueOrDefault(uid, aa.Aim);
            // Сервер шлёт угол 10 раз в секунду — догоняем его плавно, без рывков.
            var diff = Angle.ShortestDistance(shown, aa.Aim);
            var step = Math.Min(1f, frameTime * 12f);
            shown += new Angle(diff.Theta * step);
            _shown[uid] = shown;
            _sprite.LayerSetRotation((uid, sprite), TurretLayer, shown);
        }
    }
}

public sealed class CadiaAntiAirBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CadiaAntiAirWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CadiaAntiAirWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnAim += offset => SendMessage(new CadiaAntiAirAimMessage(offset));
        _window.OnLevel += level => SendMessage(new CadiaAntiAirLevelMessage(level));
        _window.OnFire += firing => SendMessage(new CadiaAntiAirFireMessage(firing));
        _window.OnView += grid => SendMessage(new CadiaAntiAirViewMessage(grid));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CadiaAntiAirBuiState aa)
            _window?.Update(aa);
    }
}

/// <summary>Огневой пост зенитки: радар неба слева, этаж, лента и спуск справа.</summary>
public sealed class CadiaAntiAirWindow : DefaultWindow
{
    public event Action<Vector2>? OnAim;
    public event Action<int>? OnLevel;
    public event Action<bool>? OnFire;
    public event Action<NetEntity?>? OnView;

    private readonly BoxContainer _contacts = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly Button _stopView = new();

    private readonly CadiaSkyRadarControl _radar = new();
    private readonly Label _gun = new() { FontColorOverride = Color.FromHex("#e8c66a") };
    private readonly Label _status = new();
    private readonly Label _ammo = new();
    private readonly ProgressBar _ammoBar = new() { MinHeight = 14, MinValue = 0, MaxValue = 1 };
    private readonly Label _auspex = new();
    private readonly Button _levelAir = new() { ToggleMode = true };
    private readonly Button _levelOrbit = new() { ToggleMode = true };
    private readonly Button _fire = new() { MinHeight = 64 };
    private readonly RichTextLabel _hint = new();

    private CadiaAntiAirBuiState? _state;
    private string _contactsKey = string.Empty;
    private DateTime _contactsRebuild;

    public CadiaAntiAirWindow()
    {
        MinSize = SetSize = new Vector2(1180, 620);
        _stopView.Text = Loc.GetString("cadia-aa-ui-stop-view");
        _stopView.OnPressed += _ => OnView?.Invoke(null);

        _radar.HorizontalExpand = true;
        _radar.VerticalExpand = true;
        _radar.OnAim += offset => OnAim?.Invoke(offset);

        _levelAir.Text = Loc.GetString("cadia-aa-ui-level-air");
        _levelOrbit.Text = Loc.GetString("cadia-aa-ui-level-orbit");
        _levelAir.OnPressed += _ => OnLevel?.Invoke(1);
        _levelOrbit.OnPressed += _ => OnLevel?.Invoke(2);

        // Спуск: стреляет, пока кнопка зажата.
        _fire.Text = Loc.GetString("cadia-aa-ui-fire");
        _fire.StyleClasses.Add("Caution");
        _fire.OnButtonDown += _ => OnFire?.Invoke(true);
        _fire.OnButtonUp += _ => OnFire?.Invoke(false);

        _hint.SetMessage(FormattedMessage.FromMarkupOrThrow(Loc.GetString("cadia-aa-ui-hint")), defaultColor: Color.FromHex("#9a9a8a"));

        var levels = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        _levelAir.HorizontalExpand = true;
        _levelOrbit.HorizontalExpand = true;
        levels.AddChild(_levelAir);
        levels.AddChild(_levelOrbit);

        var side = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 260,
            MaxWidth = 260,
            SeparationOverride = 8,
            Margin = new Thickness(8, 0, 0, 0),
        };
        side.AddChild(_gun);
        side.AddChild(_status);
        side.AddChild(new Label { Text = Loc.GetString("cadia-aa-ui-level"), FontColorOverride = Color.FromHex("#9a9a8a") });
        side.AddChild(levels);
        side.AddChild(_ammo);
        side.AddChild(_ammoBar);
        side.AddChild(_auspex);
        side.AddChild(new Control { VerticalExpand = true });
        side.AddChild(_hint);
        side.AddChild(_fire);

        var frame = new PanelContainer
        {
            MinSize = new Vector2(440, 440),
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#040805"), BorderColor = Color.FromHex("#3d6b45"), BorderThickness = new Thickness(2) },
        };
        frame.AddChild(_radar);

        // Список бортов на радаре: силуэт, имя, этаж и дальность. Клик — камера на борт.
        var list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 240, MaxWidth = 240, SeparationOverride = 6, Margin = new Thickness(8, 0, 0, 0) };
        list.AddChild(new Label { Text = Loc.GetString("cadia-aa-ui-contacts"), FontColorOverride = Color.FromHex("#e8c66a") });
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_contacts);
        list.AddChild(scroll);
        list.AddChild(_stopView);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(6) };
        root.AddChild(frame);
        root.AddChild(list);
        root.AddChild(side);
        Contents.AddChild(root);
    }

    public void Update(CadiaAntiAirBuiState state)
    {
        _state = state;
        _radar.SetState(state);

        _gun.Text = state.Linked ? state.GunName : Loc.GetString("cadia-aa-ui-no-gun");
        var aligned = Math.Abs(Angle.ShortestDistance(state.Aim, state.TargetAim).Degrees) <= 4;
        _status.Text = !state.Linked ? string.Empty
            : state.AimPoint == null ? Loc.GetString("cadia-aa-ui-no-aim")
            : aligned ? Loc.GetString("cadia-aa-ui-on-target") : Loc.GetString("cadia-aa-ui-traversing");
        _status.FontColorOverride = aligned && state.AimPoint != null ? Color.FromHex("#6dff8a") : Color.FromHex("#ff9a3c");

        _levelAir.Pressed = state.AimLevel == 1;
        _levelOrbit.Pressed = state.AimLevel == 2;
        _levelOrbit.Disabled = state.Levels < 2;

        _ammo.Text = Loc.GetString("cadia-aa-ui-ammo", ("ammo", state.Ammo), ("max", state.MaxAmmo));
        _ammoBar.Value = state.MaxAmmo == 0 ? 0 : (float) state.Ammo / state.MaxAmmo;
        _auspex.Text = Loc.GetString(state.AuspexLinked ? "cadia-aa-ui-auspex-on" : "cadia-aa-ui-auspex-off");
        _auspex.FontColorOverride = state.AuspexLinked ? Color.FromHex("#6dff8a") : Color.FromHex("#9a9a8a");

        _fire.Disabled = !state.Linked || state.AimPoint == null || state.Ammo <= 0 || state.OutOfRange;
        if (state.OutOfRange)
        {
            _status.Text = Loc.GetString("cadia-aa-ui-out-of-range");
            _status.FontColorOverride = Color.FromHex("#ff5a4a");
        }

        _stopView.Visible = state.Viewing != null;
        var key = string.Join(";", state.Contacts.Select(c => $"{c.Grid}:{c.Level}:{c.Kind}")) + $"|{state.Viewing}|{state.AimGrid}";
        if (key == _contactsKey && DateTime.UtcNow < _contactsRebuild)
            return;
        _contactsKey = key;
        _contactsRebuild = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        _contacts.RemoveAllChildren();
        if (state.Contacts.Count == 0)
            _contacts.AddChild(new Label { Text = Loc.GetString("cadia-aa-ui-sky-clear"), FontColorOverride = Color.FromHex("#9a9a8a") });
        foreach (var contact in state.Contacts)
            _contacts.AddChild(ContactButton(contact, state));
    }

    private Control ContactButton(CadiaAirContact contact, CadiaAntiAirBuiState state)
    {
        var color = contact.Kind switch
        {
            CadiaAirContactKind.Enemy => Color.FromHex("#ff3a2a"),
            CadiaAirContactKind.Friendly => Color.FromHex("#5aff78"),
            _ => Color.FromHex("#9a9a8a"),
        };
        var button = new Button { ToggleMode = true, Pressed = state.Viewing == contact.Grid, MinHeight = 52 };
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        row.AddChild(new CadiaShipSilhouette(contact, color) { MinSize = new Vector2(44, 44) });
        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        text.AddChild(new Label { Text = contact.Name, FontColorOverride = color, ClipText = true });
        var level = Loc.GetString(contact.Level == 1 ? "cadia-aa-ui-tag-air" : "cadia-aa-ui-tag-orbit");
        var locked = state.AimGrid == contact.Grid ? " • " + Loc.GetString("cadia-aa-ui-locked") : string.Empty;
        text.AddChild(new Label { Text = $"{level}, {(int) contact.Offset.Length()} м{locked}", FontColorOverride = Color.FromHex("#b8c8b8") });
        row.AddChild(text);
        button.AddChild(row);
        button.OnPressed += _ => OnView?.Invoke(contact.Grid);
        return button;
    }

    public override void Close()
    {
        OnFire?.Invoke(false);
        base.Close();
    }
}

/// <summary>
/// Радар неба: позиция в центре, кольца дальности, бегущий луч развёртки, борта над позицией (красные — враг,
/// зелёные — свои, серые — пустые), разрывы последних секунд, курс башни и точка прицела. Клик — навести.
/// </summary>
public sealed class CadiaSkyRadarControl : Control
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color Phosphor = Color.FromHex("#5aff78");
    private static readonly Color Dim = Color.FromHex("#1f4a2a");
    private static readonly Color Enemy = Color.FromHex("#ff3a2a");
    private static readonly Color Friendly = Color.FromHex("#5aff78");
    private static readonly Color Neutral = Color.FromHex("#9a9a8a");
    private static readonly Color AimColor = Color.FromHex("#ffd37a");

    public event Action<Vector2>? OnAim;

    private CadiaAntiAirBuiState? _state;
    private readonly Font _font;

    public CadiaSkyRadarControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 11);
    }

    public void SetState(CadiaAntiAirBuiState state)
    {
        _state = state;
    }

    private (Vector2 Center, float Radius, float Scale) Layout()
    {
        var center = PixelSize / 2f;
        var radius = MathF.Min(PixelSize.X, PixelSize.Y) / 2f - 14f;
        var range = _state is { Range: > 0 } ? _state.Range : 1f;
        return (center, radius, radius / range);
    }

    private Vector2 ToScreen(Vector2 offset)
    {
        var (center, _, scale) = Layout();
        return center + new Vector2(offset.X, -offset.Y) * scale;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick || _state is not { Linked: true })
            return;
        var (center, _, scale) = Layout();
        var local = (args.RelativePixelPosition - center) / scale;
        OnAim?.Invoke(new Vector2(local.X, -local.Y));
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var (center, radius, scale) = Layout();

        if (_state is not { Linked: true })
        {
            var text = Loc.GetString("cadia-aa-ui-no-gun");
            handle.DrawString(_font, center - handle.GetDimensions(_font, text, 1f) / 2f, text, Enemy);
            return;
        }

        // Экран: кольца дальности, перекрестие, стороны света.
        handle.DrawCircle(center, radius, Color.FromHex("#061208"));
        for (var i = 1; i <= 4; i++)
            handle.DrawCircle(center, radius * i / 4f, Dim, false);
        handle.DrawLine(center - new Vector2(radius, 0), center + new Vector2(radius, 0), Dim);
        handle.DrawLine(center - new Vector2(0, radius), center + new Vector2(0, radius), Dim);
        foreach (var (label, dir) in new[] { ("С", new Vector2(0, -1)), ("Ю", new Vector2(0, 1)), ("В", new Vector2(1, 0)), ("З", new Vector2(-1, 0)) })
        {
            var size = handle.GetDimensions(_font, label, 1f);
            handle.DrawString(_font, center + dir * (radius + 7) - size / 2f, label, Phosphor.WithAlpha(0.6f));
        }

        // Луч развёртки с затухающим хвостом.
        var t = (float) (_timing.RealTime.TotalSeconds % 2.5 / 2.5) * MathF.Tau;
        for (var k = 0; k < 14; k++)
        {
            var a = t - k * 0.035f;
            var end = center + new Vector2(MathF.Sin(a), -MathF.Cos(a)) * radius;
            handle.DrawLine(center, end, Phosphor.WithAlpha(0.35f * (1 - k / 14f)));
        }

        // Курс башни сейчас и куда она доворачивает.
        var turret = _state.Aim.RotateVec(new Vector2(0, 1));
        handle.DrawLine(center, ToScreen(turret * _state.Range * 0.35f), AimColor);
        var target = _state.TargetAim.RotateVec(new Vector2(0, 1));
        handle.DrawLine(center, ToScreen(target * _state.Range), AimColor.WithAlpha(0.25f));

        // Борта: размер отметки — по размеру шаттла, номер этажа рядом.
        foreach (var contact in _state.Contacts)
        {
            var p = ToScreen(contact.Offset);
            var color = contact.Kind switch
            {
                CadiaAirContactKind.Enemy => Enemy,
                CadiaAirContactKind.Friendly => Friendly,
                _ => Neutral,
            };
            var r = Math.Max(5f, contact.Size * scale);
            handle.DrawCircle(p, r, color.WithAlpha(0.25f));
            handle.DrawCircle(p, r, color, false);
            var tag = Loc.GetString(contact.Level == 1 ? "cadia-aa-ui-tag-air" : "cadia-aa-ui-tag-orbit");
            handle.DrawString(_font, p + new Vector2(r + 3, -7), $"{contact.Name} [{tag}]", color);
        }

        // Разрывы: жёлтые — мимо, красные — в цель.
        foreach (var burst in _state.Bursts)
        {
            var p = ToScreen(burst.Offset);
            var color = burst.Hit ? Enemy : AimColor;
            handle.DrawLine(p - new Vector2(4, 4), p + new Vector2(4, 4), color);
            handle.DrawLine(p - new Vector2(4, -4), p + new Vector2(4, -4), color);
        }

        // Прицел.
        if (_state.AimPoint is { } aim)
        {
            var p = ToScreen(aim);
            handle.DrawCircle(p, 9, AimColor, false);
            handle.DrawLine(p - new Vector2(14, 0), p - new Vector2(5, 0), AimColor);
            handle.DrawLine(p + new Vector2(5, 0), p + new Vector2(14, 0), AimColor);
            handle.DrawLine(p - new Vector2(0, 14), p - new Vector2(0, 5), AimColor);
            handle.DrawLine(p + new Vector2(0, 5), p + new Vector2(0, 14), AimColor);
        }

        // Сама позиция.
        handle.DrawRect(UIBox2.FromDimensions(center - new Vector2(4), new Vector2(8)), Phosphor);
        if (_state.Firing)
            handle.DrawString(_font, new Vector2(8, 6), Loc.GetString("cadia-aa-ui-firing"), Enemy);
    }
}

/// <summary>Силуэт борта по его плиткам, цветом стороны.</summary>
public sealed class CadiaShipSilhouette(CadiaAirContact contact, Color color) : Control
{
    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (contact.SilhouetteWidth == 0)
            return;
        var cell = MathF.Min(PixelSize.X / contact.SilhouetteWidth, PixelSize.Y / contact.SilhouetteHeight);
        var origin = (PixelSize - new Vector2(contact.SilhouetteWidth, contact.SilhouetteHeight) * cell) / 2f;
        for (var y = 0; y < contact.SilhouetteHeight; y++)
        {
            for (var x = 0; x < contact.SilhouetteWidth; x++)
            {
                if (contact.Silhouette[y * contact.SilhouetteWidth + x] == 0)
                    continue;
                // Сетка снизу вверх, экран сверху вниз.
                var pos = origin + new Vector2(x * cell, (contact.SilhouetteHeight - 1 - y) * cell);
                handle.DrawRect(UIBox2.FromDimensions(pos, new Vector2(MathF.Max(1, cell - 0.5f))), color);
            }
        }
    }
}