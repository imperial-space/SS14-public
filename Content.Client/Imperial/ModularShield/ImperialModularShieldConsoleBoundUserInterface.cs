using System.Numerics;
using Content.Client.Imperial.Fishing;
using Content.Shared.Imperial.ModularShield;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.ModularShield;

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
