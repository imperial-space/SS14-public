using System.Globalization;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.BluespaceTap;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.BluespaceTap;

[UsedImplicitly]
public sealed class BluespaceTapBoundUserInterface : BoundUserInterface
{
    private BluespaceTapWindow? _window;

    public BluespaceTapBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BluespaceTapWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BluespaceTapUiState tap)
            _window?.UpdateState(tap);
    }
}

/// <summary>Окно блюспейс-сборщика 1 в 1 с tgui BluespaceTap из Paradise (650×450).</summary>
public sealed class BluespaceTapWindow : DefaultWindow
{
    private const float MW = 1000000f;

    public event Action<BoundUserInterfaceMessage>? OnMessage;

    private readonly BoxContainer _root = Tgui.VBox(8);
    private bool _inputExpanded;
    private BluespaceTapUiState? _state;

    public BluespaceTapWindow()
    {
        Title = Loc.GetString("bluespace-tap-ui-title");
        MinSize = SetSize = new Vector2(650, 450);
        _root.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(_root));
    }

    private void Send(BoundUserInterfaceMessage message)
    {
        OnMessage?.Invoke(message);
    }

    /// <summary>formatPower из tgui-core.</summary>
    private static string FormatPower(float watts)
    {
        string[] units = { "W", "kW", "MW", "GW", "TW" };
        var value = watts;
        var unit = 0;
        while (Math.Abs(value) >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }

        return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {units[unit]}";
    }

    public void UpdateState(BluespaceTapUiState state)
    {
        _state = state;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_state is not { } state)
            return;

        _root.RemoveAllChildren();

        if (state.Portaling)
            _root.AddChild(Incursion());

        // Alerts.
        if (!state.AutoShutdown && !state.Emagged)
            _root.AddChild(Tgui.NoticeBox(Loc.GetString("bluespace-tap-ui-auto-shutdown-disabled"), Tgui.Red));

        if (state.Emagged)
            _root.AddChild(Tgui.NoticeBox(Loc.GetString("bluespace-tap-ui-safeties-disabled"), Tgui.Red));
        else if (state.MiningPower > 15 * MW)
        {
            if (!state.Stabilizers)
                _root.AddChild(Tgui.NoticeBox(Loc.GetString("bluespace-tap-ui-stabilizers-disabled"), Tgui.Red));
            else if (state.MiningPower > state.StabilizerPower + 15 * MW)
                _root.AddChild(Tgui.NoticeBox(Loc.GetString("bluespace-tap-ui-stabilizers-overwhelmed"), Tgui.Red));
            else
                _root.AddChild(Tgui.NoticeBox(Loc.GetString("bluespace-tap-ui-stabilizers-engaged")));
        }

        // Collapsible «Input Management».
        _root.AddChild(Tgui.Button((_inputExpanded ? "▼  " : "►  ") + Loc.GetString("bluespace-tap-ui-input-management"), () =>
        {
            _inputExpanded = !_inputExpanded;
            Rebuild();
        }, fluid: true, bold: true));

        if (_inputExpanded)
            _root.AddChild(BuildInput(state));

        _root.AddChild(BuildOutput(state));
    }

    /// <summary>Incursion: красный экран с черепом и искажённым текстом ошибки.</summary>
    private static Control Incursion()
    {
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#230000"), BorderColor = Tgui.Red, BorderThickness = new Thickness(2) },
            Children =
            {
                Tgui.VBox(2,
                    Tgui.Text("☠", Tgui.Red, 40, true, Control.HAlignment.Center),
                    Tgui.Text(Loc.GetString("bluespace-tap-ui-incursion"), Tgui.Red, 20, true, Control.HAlignment.Center)),
            },
        };
    }

    private Control Toggle(string text, bool on, bool emagged, string tooltip, BoundUserInterfaceMessage message)
    {
        var active = on && !emagged;
        return Tgui.Button((active ? "◉ " : "○ ") + text, () => Send(message),
            color: active ? Tgui.Green : Tgui.Red, disabled: emagged, tooltip: tooltip);
    }

    private Control PowerButton(string text, string tooltip, bool disabled, float power)
    {
        return Tgui.Button(text, () => Send(new BluespaceTapSetPowerMessage(power)), disabled: disabled, tooltip: tooltip, bold: true);
    }

    private Control BuildInput(BluespaceTapUiState state)
    {
        var desired = state.DesiredMiningPower;
        var atZero = desired == 0 || state.Emagged;

        var input = new LineEdit
        {
            MinWidth = 110,
            Text = ((long) desired).ToString(CultureInfo.InvariantCulture),
            Editable = !state.Emagged,
        };
        input.OnTextEntered += args =>
        {
            if (float.TryParse(args.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                Send(new BluespaceTapSetPowerMessage(value));
        };

        var setter = Tgui.HBox(2,
            PowerButton("|◄", Loc.GetString("bluespace-tap-ui-set-zero"), atZero, 0),
            PowerButton("◄◄", Loc.GetString("bluespace-tap-ui-minus-ten"), atZero, desired - 10 * MW),
            PowerButton("◄", Loc.GetString("bluespace-tap-ui-minus-one"), atZero, desired - MW),
            input,
            PowerButton("►", Loc.GetString("bluespace-tap-ui-plus-one"), state.Emagged, desired + MW),
            PowerButton("►►", Loc.GetString("bluespace-tap-ui-plus-ten"), state.Emagged, desired + 10 * MW));

        var barColor = state.DesiredMiningPower > state.MiningPower ? Tgui.Bad : Tgui.Good;
        var content = Tgui.VBox(8,
            Tgui.HBox(4,
                Toggle(Loc.GetString("bluespace-tap-ui-auto-shutdown"), state.AutoShutdown, state.Emagged,
                    Loc.GetString("bluespace-tap-ui-auto-shutdown-tip"), new BluespaceTapToggleAutoShutdownMessage()),
                Toggle(Loc.GetString("bluespace-tap-ui-stabilizers"), state.Stabilizers, state.Emagged,
                    Loc.GetString("bluespace-tap-ui-stabilizers-tip"), new BluespaceTapToggleStabilizersMessage()),
                Toggle(Loc.GetString("bluespace-tap-ui-stabilizer-priority"), state.StabilizerPriority, state.Emagged,
                    Loc.GetString("bluespace-tap-ui-stabilizer-priority-tip"), new BluespaceTapToggleStabilizerPriorityMessage())),
            Tgui.LabeledList(
                (Loc.GetString("bluespace-tap-ui-desired-power"), Tgui.Text(FormatPower(desired), barColor, bold: true)),
                (Loc.GetString("bluespace-tap-ui-set-desired-power"), setter),
                (Loc.GetString("bluespace-tap-ui-total-power"), Tgui.Text(FormatPower(state.PowerUse))),
                (Loc.GetString("bluespace-tap-ui-mining-power"), Tgui.Text(FormatPower(state.MiningPower))),
                (Loc.GetString("bluespace-tap-ui-stabilizer-power"), Tgui.Text(FormatPower(state.StabilizerPower))),
                (Loc.GetString("bluespace-tap-ui-surplus-power"), Tgui.Text(FormatPower(state.AvailablePower)))));

        return Tgui.MakeSection(Loc.GetString("bluespace-tap-ui-input"), null, content);
    }

    private Control BuildOutput(BluespaceTapUiState state)
    {
        var points = Tgui.LabeledList(
            (Loc.GetString("bluespace-tap-ui-available-points"), Tgui.Text(((long) state.Points).ToString(), Tgui.Good, 14, true)),
            (Loc.GetString("bluespace-tap-ui-total-points"), Tgui.Text(((long) state.TotalPoints).ToString(), size: 14)));
        points.HorizontalExpand = true;

        var products = new GridContainer { Columns = 2, HSeparationOverride = 8, VSeparationOverride = 4 };
        foreach (var product in state.Products)
        {
            var key = product.Key;
            var buy = Tgui.Button(product.Price.ToString(), () => Send(new BluespaceTapVendMessage(key)),
                disabled: product.Price >= state.Points, bold: true);
            buy.MinWidth = 70;
            Tgui.AddItem(products, product.Name, buy);
        }

        Control content = Tgui.HBox(12, points, products);
        if (state.Dirty)
        {
            content = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#3f2712") },
                Children =
                {
                    Tgui.VBox(2,
                        Tgui.Text(Loc.GetString("bluespace-tap-ui-blockage"), Tgui.Brown, 20, true, Control.HAlignment.Center),
                        Tgui.Text(Loc.GetString("bluespace-tap-ui-cleanup"), Tgui.Brown, 20, true, Control.HAlignment.Center)),
                },
            };
        }

        return Tgui.MakeSection(Loc.GetString("bluespace-tap-ui-output"), null, content);
    }
}
