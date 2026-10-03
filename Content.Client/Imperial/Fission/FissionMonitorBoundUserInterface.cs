using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Imperial.Fission;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Fission;

[UsedImplicitly]
public sealed class FissionMonitorBoundUserInterface : BoundUserInterface
{
    private FissionMonitorWindow? _window;

    public FissionMonitorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FissionMonitorWindow>();
        _window.OnThrottle += value => SendMessage(new FissionSetThrottleMessage(value));
        _window.OnVent += () => SendMessage(new FissionToggleVentMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FissionMonitorUiState monitor)
            _window?.UpdateState(monitor);
    }
}

/// <summary>
/// Консоль NGCR 1 в 1 с tgui ReactorMonitor (SS220): окно 550×500, слева «Metrics» шириной 270
/// с шестью полосками и ручкой Knob size=5, справа «Gases» с кнопкой клапана и «Moderator Gases».
/// </summary>
public sealed class FissionMonitorWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public event Action<float>? OnThrottle;
    public event Action? OnVent;

    private readonly GridContainer _metrics = new() { Columns = 2, HSeparationOverride = 8, VSeparationOverride = 4 };
    private readonly GridContainer _gases = GasGrid();
    private readonly GridContainer _moderatorGases = GasGrid();
    private readonly TguiKnob _throttle;
    private readonly TguiButton _ventButton;
    private readonly BoxContainer _content;

    public FissionMonitorWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = Loc.GetString("fission-monitor-title");
        SetSize = new Vector2(550, 500);
        MinSize = new Vector2(550, 400);

        _throttle = new TguiKnob(5)
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 1,
            StepPixelSize = 2,
            Unit = "%",
            HorizontalAlignment = HAlignment.Center,
        };
        _throttle.OnChange += value => OnThrottle?.Invoke(value);

        var knobSection = Tgui.MakeSection(Loc.GetString("fission-monitor-desired-limit"), null, _throttle);
        var metrics = Tgui.MakeSection(Loc.GetString("fission-monitor-metrics"), null, Tgui.VBox(8, _metrics, knobSection));
        metrics.MinWidth = 270;
        metrics.MaxWidth = 270;

        _ventButton = Tgui.Button(string.Empty, () => OnVent?.Invoke(), icon: "power-off");
        var gasesSection = Tgui.MakeSection(Loc.GetString("fission-monitor-gases"), _ventButton, _gases);
        gasesSection.VerticalExpand = true;
        var moderatorSection = Tgui.MakeSection(Loc.GetString("fission-monitor-moderator-gases"), null, _moderatorGases);
        moderatorSection.VerticalExpand = true;
        var right = Tgui.VBox(8, gasesSection, moderatorSection);
        right.HorizontalExpand = true;

        _content = Tgui.HBox(8, metrics, right);
        _content.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(_content));
    }

    private static GridContainer GasGrid() => new() { Columns = 2, HSeparationOverride = 8, VSeparationOverride = 4, HorizontalExpand = true };

    /// <summary>logScale из ReactorMonitor: log2(16 + x) − 4.</summary>
    private static float LogScale(float value) => MathF.Log2(16 + MathF.Max(0, value)) - 4;

    /// <summary>ranges tgui: первый подходящий диапазон.</summary>
    private static Color Range(float value, params (string Color, float From, float To)[] ranges)
    {
        foreach (var (color, from, to) in ranges)
        {
            if (value >= from && value <= to)
                return TguiColors.Get(color);
        }

        return TguiColors.Get("default");
    }

    private static string Fixed(float value) => MathF.Round(value).ToString(CultureInfo.InvariantCulture);

    public void UpdateState(FissionMonitorUiState state)
    {
        // Без реактора ui_data пуст: tgui показывает окно без данных.
        _content.Visible = state.HasReactor;
        if (!state.HasReactor)
            return;

        _metrics.RemoveAllChildren();
        var integrity = state.Integrity / 100;
        AddMetric("fission-monitor-integrity", integrity,
            Range(integrity, ("good", 0.9f, float.PositiveInfinity), ("average", 0.5f, 0.9f), ("bad", float.NegativeInfinity, 0.5f)),
            $"{Fixed(integrity * 100)}%");

        var power = state.PowerKilowatts;
        var powerColor = power <= 10000
            ? Range(power, ("good", 400, float.PositiveInfinity), ("average", 200, 400), ("bad", float.NegativeInfinity, 200))
            : Tgui.Good;
        AddMetric("fission-monitor-power", power / 2000, powerColor,
            power < 10000 ? $"{Fixed(power)} KW" : $"{Fixed(power / 1000)} MW");

        var coefficient = state.Coefficient;
        AddMetric("fission-monitor-coefficient", (coefficient - 1) / 4.25f,
            Range(coefficient, ("bad", 1, 1.55f), ("average", 1.55f, 5.25f), ("good", 5.25f, float.PositiveInfinity)),
            coefficient.ToString("0.00", CultureInfo.InvariantCulture));

        var temperature = LogScale(state.Temperature);
        AddMetric("fission-monitor-temperature", temperature / LogScale(10000),
            Range(temperature, ("teal", float.NegativeInfinity, LogScale(80)), ("good", LogScale(80), LogScale(373)),
                ("average", LogScale(373), LogScale(1000)), ("bad", LogScale(1000), float.PositiveInfinity)),
            $"{Fixed(state.Temperature)} K");

        var pressure = LogScale(state.Pressure);
        AddMetric("fission-monitor-pressure", pressure / LogScale(50000),
            Range(pressure, ("good", LogScale(1), LogScale(1000)), ("average", float.NegativeInfinity, LogScale(3000)),
                ("bad", LogScale(3000), float.PositiveInfinity)),
            $"{Fixed(state.Pressure)} kPa");

        var limiter = state.OperatingPower;
        AddMetric("fission-monitor-limiter", limiter / 100,
            Range(limiter, ("teal", 100, float.PositiveInfinity), ("good", 70, 99), ("average", 30, 70), ("bad", float.NegativeInfinity, 30)),
            $"{Fixed(limiter)} %");

        _throttle.Value = state.Throttle;

        // Button icon=power-off, selected при открытом клапане.
        _ventButton.TextLabel.Text = Loc.GetString(state.Venting ? "fission-monitor-vent-open" : "fission-monitor-vent-closed");
        _ventButton.SetIcon("power-off");
        _ventButton.Color = state.Venting ? Tgui.Green : Tgui.Primary;

        FillGases(_gases, state.Gases);
        FillGases(_moderatorGases, state.ModeratorGases);
    }

    private void AddMetric(string label, float ratio, Color color, string text)
    {
        _metrics.AddChild(Tgui.Text(Loc.GetString(label) + ":", Tgui.Label));
        var bar = Tgui.ProgressBar(ratio, color, text);
        bar.HorizontalExpand = true;
        _metrics.AddChild(bar);
    }

    /// <summary>Газы с amount ≥ 0.01 по убыванию; подпись — HoverHelp/HelpDummy и getGasLabel.</summary>
    private void FillGases(GridContainer list, List<FissionGasEntry> gases)
    {
        list.RemoveAllChildren();
        var filtered = gases.Where(g => g.Amount >= 0.01f).OrderByDescending(g => g.Amount).ToList();
        var max = MathF.Max(1, filtered.Count > 0 ? filtered.Max(g => g.Portion) : 1);
        foreach (var gas in filtered)
        {
            var name = gas.Gas;
            var color = Tgui.PrimaryLine;
            if (_proto.TryIndex<GasPrototype>(gas.Gas, out var proto))
            {
                name = PlainDigits(Loc.GetString(proto.Abbreviation));
                color = proto.Color;
            }

            var icon = TguiIcons.Icon("question-circle", 12, Tgui.Label);
            icon.MinWidth = 12;
            if (gas.Description != null)
            {
                icon.ToolTip = Loc.GetString(gas.Description);
                icon.MouseFilter = MouseFilterMode.Stop;
            }
            else
            {
                icon.Modulate = Color.Transparent;
            }

            list.AddChild(Tgui.HBox(6, icon, Tgui.Text(name + ":", Tgui.Label)));
            var bar = Tgui.ProgressBar(gas.Portion / max, color,
                $"{Fixed(gas.Amount)} mol ({gas.Portion.ToString("0.##", CultureInfo.InvariantCulture)}%)");
            bar.HorizontalExpand = true;
            list.AddChild(bar);
        }
    }

    private static string PlainDigits(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] >= '₀' && chars[i] <= '₉')
                chars[i] = (char) ('0' + (chars[i] - '₀'));
        }

        return new string(chars);
    }
}

/// <summary>show_radial_menu центрифуги: выбор продукта обогащения.</summary>
[UsedImplicitly]
public sealed class FissionCentrifugeBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private Content.Client.UserInterface.Controls.SimpleRadialMenu? _menu;

    public FissionCentrifugeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<Content.Client.UserInterface.Controls.SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not FissionCentrifugeUiState centrifuge || _menu == null)
            return;

        var options = new List<Content.Client.UserInterface.Controls.RadialMenuOptionBase>();
        foreach (var result in centrifuge.Options)
        {
            var name = _proto.TryIndex<EntityPrototype>(result, out var proto) ? proto.Name : result;
            options.Add(new Content.Client.UserInterface.Controls.RadialMenuActionOption<string>(Pick, result)
            {
                IconSpecifier = Content.Client.UserInterface.Controls.RadialMenuIconSpecifier.With(new EntProtoId(result)),
                ToolTip = name,
            });
        }

        _menu.SetButtons(options);
    }

    private void Pick(string result)
    {
        SendMessage(new FissionCentrifugeChooseMessage(result));
        Close();
    }
}
