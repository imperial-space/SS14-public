using System.Numerics;
using Content.Client.Imperial.Fishing;
using Content.Shared.Imperial.ModularShield;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.ModularShield;

internal static class ShieldUi
{
    /// <summary>ProgressBar с диапазонами good ≥ 75%, average 25–75%, bad &lt; 25% и подписью поверх.</summary>
    public static (Control Root, ProgressBar Bar, Label Text) Bar()
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            MinHeight = 22,
            HorizontalExpand = true,
            BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1b1b1f") },
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#5baa27") },
        };
        var text = new Label { HorizontalAlignment = Control.HAlignment.Center, VerticalAlignment = Control.VAlignment.Center };
        var root = new Control { HorizontalExpand = true, Margin = new Thickness(0, 2) };
        root.AddChild(bar);
        root.AddChild(text);
        return (root, bar, text);
    }

    public static void Set(ProgressBar bar, Label label, float value, float max, string text)
    {
        var fraction = max <= 0 ? 0 : Math.Clamp(value / max, 0, 1);
        bar.Value = fraction;
        var color = fraction >= 0.75f ? "#5baa27" : fraction >= 0.25f ? "#d09000" : "#bd2020";
        bar.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex(color) };
        label.Text = text;
    }

    public static string Num(float value) => MathF.Round(value, 2).ToString();
}

/// <summary>Окно генератора модульного щита (tgui ModularShieldGen).</summary>
public sealed class ImperialModularShieldGeneratorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private Label _strengthTitle = default!;
    private (Control Root, ProgressBar Bar, Label Text) _strength;
    private (Control Root, ProgressBar Bar, Label Text) _regen;
    private (Control Root, ProgressBar Bar, Label Text) _radius;
    private Label _radiusValue = default!;
    private Button _radiusMinus = default!;
    private Button _radiusPlus = default!;
    private Button _exterior = default!;
    private Button _power = default!;
    private ImperialModularShieldGeneratorState? _state;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-modular-shield-ui-title");
        _window.SetSize = new Vector2(690, 260);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };

        var left = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SizeFlagsStretchRatio = 2 };
        _strength = ShieldUi.Bar();
        var strengthSection = ImperialFishingUi.Section(Loc.GetString("imperial-modular-shield-ui-strength"), _strength.Root);
        _strengthTitle = FindHeading(strengthSection)!;
        left.AddChild(strengthSection);

        var regenBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _regen = ShieldUi.Bar();
        _radius = ShieldUi.Bar();
        regenBox.AddChild(_regen.Root);
        regenBox.AddChild(_radius.Root);
        left.AddChild(ImperialFishingUi.Section(Loc.GetString("imperial-modular-shield-ui-regen-radius"), regenBox));
        root.AddChild(left);

        var right = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SizeFlagsStretchRatio = 1 };
        var settings = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var radiusRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        _radiusMinus = new Button { Text = "-", StyleClasses = { "ButtonSquare" } };
        _radiusMinus.OnPressed += _ => SetRadius(-1);
        _radiusValue = new Label { MinWidth = 50, Align = Label.AlignMode.Center };
        _radiusPlus = new Button { Text = "+", StyleClasses = { "ButtonSquare" } };
        _radiusPlus.OnPressed += _ => SetRadius(1);
        radiusRow.AddChild(_radiusMinus);
        radiusRow.AddChild(_radiusValue);
        radiusRow.AddChild(_radiusPlus);
        settings.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-modular-shield-ui-set-radius"), radiusRow, 100));

        _exterior = new Button { StyleClasses = { "ButtonSquare" } };
        _exterior.OnPressed += _ => SendMessage(new ImperialModularShieldToggleExteriorMessage());
        settings.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-modular-shield-ui-limitations"), _exterior, 100));
        right.AddChild(ImperialFishingUi.Section(Loc.GetString("imperial-modular-shield-ui-settings"), settings));

        _power = new Button { ToggleMode = true };
        _power.OnPressed += _ => SendMessage(new ImperialModularShieldToggleMessage());
        right.AddChild(ImperialFishingUi.Section(null, ImperialFishingUi.LabeledItem(Loc.GetString("imperial-modular-shield-ui-power"), _power, 100)));
        root.AddChild(right);

        _window.Contents.AddChild(root);
        if (_state != null)
            Apply(_state);
    }

    private static Label? FindHeading(Control control)
    {
        foreach (var child in control.Children)
        {
            if (child is Label label && label.StyleClasses.Contains("LabelHeading"))
                return label;
            if (FindHeading(child) is { } found)
                return found;
        }

        return null;
    }

    private void SetRadius(int delta)
    {
        if (_state == null)
            return;

        var radius = Math.Clamp(MathF.Round(_state.CurrentRadius) + delta, 3, MathF.Max(3, _state.MaxRadius));
        SendMessage(new ImperialModularShieldSetRadiusMessage(radius));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialModularShieldGeneratorState gen)
            return;

        _state = gen;
        if (_window != null)
            Apply(gen);
    }

    private void Apply(ImperialModularShieldGeneratorState s)
    {
        _strengthTitle.FontColorOverride = s.Recovering ? Color.Red : Color.White;
        ShieldUi.Set(_strength.Bar, _strength.Text, s.CurrentStrength, s.MaxStrength, $"{ShieldUi.Num(s.CurrentStrength)}/{ShieldUi.Num(s.MaxStrength)}");
        ShieldUi.Set(_regen.Bar, _regen.Text, s.CurrentRegeneration, s.MaxRegeneration,
            Loc.GetString("imperial-modular-shield-ui-regen", ("current", ShieldUi.Num(s.CurrentRegeneration)), ("max", ShieldUi.Num(s.MaxRegeneration))));
        ShieldUi.Set(_radius.Bar, _radius.Text, s.CurrentRadius, s.MaxRadius,
            Loc.GetString("imperial-modular-shield-ui-radius", ("current", ShieldUi.Num(s.CurrentRadius)), ("max", ShieldUi.Num(s.MaxRadius))));

        _radiusValue.Text = ShieldUi.Num(s.CurrentRadius);
        _radiusMinus.Disabled = s.Active;
        _radiusPlus.Disabled = s.Active;
        _exterior.Disabled = s.Active;
        _exterior.Text = Loc.GetString(s.ExteriorOnly ? "imperial-modular-shield-ui-exterior" : "imperial-modular-shield-ui-interior");
        _power.Disabled = s.Recovering || s.Initiating;
        _power.Pressed = s.Active;
        _power.Text = Loc.GetString(s.Active ? "imperial-modular-shield-ui-on" : "imperial-modular-shield-ui-off");
    }
}

/// <summary>Консоль модульных щитов (tgui ModularShieldConsole).</summary>
public sealed class ImperialModularShieldConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private BoxContainer? _table;
    private string? _signature;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-modular-shield-console-title");
        _window.SetSize = new Vector2(480, 300);
        _table = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(4) };
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_table);
        _window.Contents.AddChild(scroll);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialModularShieldConsoleState console || _table == null)
            return;

        // Перестраивать только при изменении состава, чтобы не сбивать ввод имени.
        var signature = string.Join("|", console.Generators.ConvertAll(g => $"{g.Id}:{g.Name}"));
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild(console);
        }

        Refresh(console);
    }

    private readonly List<(ProgressBar Bar, Label Text, Button Toggle)> _rows = new();

    private void Rebuild(ImperialModularShieldConsoleState console)
    {
        _table!.RemoveAllChildren();
        _rows.Clear();
        if (console.Generators.Count == 0)
        {
            var notice = new PanelContainer { PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#bb3b2d").WithAlpha(0.6f) } };
            notice.AddChild(new Label { Text = Loc.GetString("imperial-modular-shield-console-empty"), Align = Label.AlignMode.Center, Margin = new Thickness(4) });
            _table.AddChild(notice);
            return;
        }

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        header.AddChild(new Label { Text = Loc.GetString("imperial-modular-shield-console-name"), MinWidth = 170, StyleClasses = { "LabelHeading" } });
        header.AddChild(new Label { Text = Loc.GetString("imperial-modular-shield-console-status"), MinWidth = 170, Align = Label.AlignMode.Center, StyleClasses = { "LabelHeading" } });
        header.AddChild(new Label { Text = Loc.GetString("imperial-modular-shield-console-toggle"), HorizontalExpand = true, Align = Label.AlignMode.Center, StyleClasses = { "LabelHeading" } });
        _table.AddChild(header);

        foreach (var gen in console.Generators)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 2) };
            var id = gen.Id;
            var name = new LineEdit { Text = gen.Name, MinWidth = 170, SetWidth = 170 };
            name.OnTextEntered += args => SendMessage(new ImperialModularShieldRenameMessage(id, args.Text));
            name.OnFocusExit += args => SendMessage(new ImperialModularShieldRenameMessage(id, args.Text));
            row.AddChild(name);

            var bar = ShieldUi.Bar();
            bar.Root.MinWidth = 170;
            bar.Root.SetWidth = 170;
            bar.Root.Margin = new Thickness(4, 0);
            row.AddChild(bar.Root);

            var toggle = new Button { ToggleMode = true, HorizontalExpand = true };
            toggle.OnPressed += _ => SendMessage(new ImperialModularShieldToggleMessage(id));
            row.AddChild(toggle);
            _table.AddChild(row);
            _rows.Add((bar.Bar, bar.Text, toggle));
        }
    }

    private void Refresh(ImperialModularShieldConsoleState console)
    {
        for (var i = 0; i < console.Generators.Count && i < _rows.Count; i++)
        {
            var gen = console.Generators[i];
            var (bar, text, toggle) = _rows[i];
            ShieldUi.Set(bar, text, gen.CurrentStrength, gen.MaxStrength,
                $"{ShieldUi.Num(gen.CurrentStrength)}/{ShieldUi.Num(gen.MaxStrength)} + {ShieldUi.Num(gen.CurrentRegeneration)}");
            text.FontColorOverride = gen.Recovering ? Color.Red : Color.White;
            toggle.Disabled = gen.Recovering;
            toggle.Pressed = gen.Active;
            toggle.Text = Loc.GetString(gen.Active ? "imperial-modular-shield-ui-on" : "imperial-modular-shield-ui-off");
        }
    }
}
