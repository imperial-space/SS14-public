using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Imperial.Hypertorus;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Hypertorus;

[UsedImplicitly]
public sealed class HypertorusBoundUserInterface : BoundUserInterface
{
    private HypertorusWindow? _window;

    public HypertorusBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<HypertorusWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is HypertorusUiState hfr)
            _window?.UpdateState(hfr);
    }
}

/// <summary>
/// Панель управления HFR 1 в 1 с tgui Hypertorus (BandaStation tgui/interfaces/Hypertorus):
/// основные переключатели и свёрнутая таблица рецептов, газы, график температур,
/// круглые датчики состояния, ручки ComboKnob и управление выводом.
/// </summary>
public sealed class HypertorusWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public event Action<BoundUserInterfaceMessage>? OnMessage;

    private const double BaseMaxTemperature = 1e8;
    private static readonly Color FusionColor = Color.FromHex("#f2711c");
    private static readonly Color ModeratorColor = Color.FromHex("#e03997");
    private static readonly Color CoolantColor = Color.FromHex("#f0f8ff");
    private static readonly Color OutputColor = Color.FromHex("#20b142");

    /// <summary>moderator_gases_help, газы SS13 заменены газами SS14.</summary>
    private static readonly Dictionary<Gas, string> ModeratorHelp = new()
    {
        [Gas.Plasma] = "hypertorus-ui-help-plasma",
        [Gas.BZ] = "hypertorus-ui-help-bz",
        [Gas.Phazonium] = "hypertorus-ui-help-proto-nitrate",
        [Gas.Oxygen] = "hypertorus-ui-help-oxygen",
        [Gas.NitrousOxide] = "hypertorus-ui-help-healium",
        [Gas.AntiNoblium] = "hypertorus-ui-help-antinoblium",
        [Gas.Frezon] = "hypertorus-ui-help-freon",
    };

    private static readonly Gas[] ModeratorSticky = { Gas.Plasma, Gas.BZ, Gas.Phazonium };

    /// <summary>recipe_effect_structure: подпись, иконка(и), масштаб, база.</summary>
    private static readonly (string Label, string[] Icons, float Scale, float Base)[] EffectStructure =
    {
        ("Cooling", new[] { "snowflake-o" }, 3, 1),
        ("Heating", new[] { "fire" }, 3, 1),
        ("Energy loss", new[] { "sun-o" }, 3, 1),
        ("Fuel use", new[] { "window-minimize", "arrow-down" }, 1.5f, 1),
        ("Production", new[] { "window-minimize", "arrow-up" }, 1.5f, 1),
        ("Max temperature", new[] { "thermometer-full" }, 1.15f, 0.85f),
    };

    private readonly BoxContainer _root = Tgui.VBox(8);

    private readonly TguiButton _powerButton;
    private readonly TguiButton _coolingButton;
    private readonly BoxContainer _recipes = Tgui.VBox(2);

    private readonly GridContainer _fusionGases = GasGrid();
    private readonly GridContainer _moderatorGases = GasGrid();
    private readonly Label _fusionNoRecipe;
    private readonly TguiButton _fuelButton;
    private readonly TguiButton _moderatorButton;
    private readonly TguiNumberInput _fuelRate;
    private readonly TguiNumberInput _moderatorRate;

    private readonly TemperatureChart _chart = new();

    private readonly TguiRoundGauge _integrityGauge = new(1.75f);
    private readonly TguiRoundGauge _ironGauge = new(1.75f);
    private readonly TguiRoundGauge _apcGauge = new(1.75f);
    private readonly TguiRoundGauge _levelGauge = new(3);
    private readonly TguiRoundGauge _energyGauge = new(1.75f);
    private readonly TguiRoundGauge _activityGauge = new(1.75f);
    private readonly TguiRoundGauge _instabilityGauge = new(1.75f);

    private readonly Dictionary<HypertorusParameter, TguiKnob> _knobs = new();

    private readonly TguiButton _wasteButton;
    private readonly TguiNumberInput _filterRate;
    private readonly GridContainer _filters = new() { Columns = 4, HSeparationOverride = 4, VSeparationOverride = 4 };

    private HypertorusUiState? _state;
    private string? _recipesBuiltFor;
    private int _recipesBuiltLevel = -1;
    private bool _startPower;

    public HypertorusWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = Loc.GetString("hypertorus-ui-title");
        MinSize = new Vector2(640, 480);
        SetSize = new Vector2(850, 980);

        _root.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(_root));

        // ── HypertorusMainControls ──
        _powerButton = Tgui.Button(string.Empty, () => Toggle(HypertorusToggle.StartPower));
        _coolingButton = Tgui.Button(string.Empty, () => Toggle(HypertorusToggle.StartCooling));
        var main = Tgui.VBox(6,
            Tgui.HBox(12,
                Tgui.HBox(4, Tgui.Text(Loc.GetString("hypertorus-ui-power"), Tgui.Label), _powerButton),
                Tgui.HBox(4, Tgui.Text(Loc.GetString("hypertorus-ui-cooling"), Tgui.Label), _coolingButton)),
            TguiLayout.Collapsible(Loc.GetString("hypertorus-ui-recipes"), _recipes));
        _root.AddChild(Tgui.MakeSection(null, null, main));

        // ── HypertorusGases ──
        _fuelButton = Tgui.Button(string.Empty, () => Toggle(HypertorusToggle.StartFuel));
        _moderatorButton = Tgui.Button(string.Empty, () => Toggle(HypertorusToggle.StartModerator));
        _fuelRate = RateInput(HypertorusParameter.FuelInjectionRate, 0.5f, 150);
        _moderatorRate = RateInput(HypertorusParameter.ModeratorInjectionRate, 0.5f, 150);
        _fusionNoRecipe = Tgui.Text(Loc.GetString("hypertorus-ui-no-recipe"), Tgui.Red, align: HAlignment.Center);

        var gases = Tgui.VBox(8,
            Tgui.MakeSection(Loc.GetString("hypertorus-ui-fusion-gases"), null, Tgui.VBox(0, _fusionNoRecipe, _fusionGases)),
            Tgui.MakeSection(Loc.GetString("hypertorus-ui-moderator-gases"), null, _moderatorGases));
        gases.HorizontalExpand = true;
        gases.MinWidth = 350;

        var temperatures = Tgui.MakeSection(Loc.GetString("hypertorus-ui-temperatures"), null, _chart);
        temperatures.HorizontalExpand = true;
        _root.AddChild(Tgui.HBox(8, gases, temperatures));

        // ── HypertorusParameters ──
        _integrityGauge.MinValue = 0;
        _integrityGauge.MaxValue = 100;
        _integrityGauge.AlertBefore = 95;
        _integrityGauge.Format = v => $"{Math.Round(v)}%";
        _integrityGauge.Ranges = new[] { ("good", 90f, 100f), ("average", 50f, 90f), ("bad", 0f, 50f) };

        _ironGauge.MaxValue = 1;
        _ironGauge.AlertAfter = 0.25f;
        _ironGauge.Format = v => $"{Math.Round(v * 100)}%";
        _ironGauge.Ranges = new[] { ("good", 0f, 0.1f), ("average", 0.1f, 0.36f), ("bad", 0.36f, 1f) };

        _apcGauge.MaxValue = 100;
        _apcGauge.AlertBefore = 30;
        _apcGauge.Format = v => $"{Math.Round(v)}%";
        _apcGauge.Ranges = new[] { ("bad", 0f, 15f), ("average", 15f, 30f), ("good", 30f, 100f) };

        _levelGauge.MaxValue = 6;
        _levelGauge.AlertAfter = 4.5f;
        _levelGauge.Format = v => Math.Round(v).ToString(CultureInfo.InvariantCulture);
        _levelGauge.Ranges = new[] { ("grey", 0f, 1f), ("good", 1f, 3.5f), ("average", 3.5f, 4.5f), ("bad", 4.5f, 6f) };

        _energyGauge.MinValue = 12;
        _energyGauge.MaxValue = 30;
        _energyGauge.Format = v => FormatSiUnit(Math.Pow(10, v), 4, "J");
        _energyGauge.Ranges = new[] { ("black", 12f, 15f), ("grey", 15f, 18f), ("yellow", 18f, 24f), ("orange", 24f, 30f) };

        _activityGauge.MaxValue = 130;
        _activityGauge.Format = v => _startPower ? $"{v.ToString("0.0", CultureInfo.InvariantCulture)}%" : "0%";
        _activityGauge.Ranges = new[] { ("grey", 0f, 70f), ("blue", 70f, 100f), ("orange", 100f, 130f) };

        _instabilityGauge.MaxValue = 10;
        _instabilityGauge.Format = v => _startPower ? $"{(v / 8 * 100).ToString("0.0", CultureInfo.InvariantCulture)}%" : "0%";
        _instabilityGauge.Ranges = new[] { ("orange", 0f, 8f), ("blue", 8f, 10f) };

        var left = Tgui.HBox(4,
            TguiLayout.LabeledControl(Loc.GetString("hypertorus-ui-integrity"), _integrityGauge),
            TguiLayout.LabeledControl(Loc.GetString("hypertorus-ui-iron"), _ironGauge),
            TguiLayout.LabeledControl(Loc.GetString("hypertorus-ui-apc"), _apcGauge));
        var middle = Tgui.HBox(4, TguiLayout.LabeledControl(Loc.GetString("hypertorus-ui-power-level"), _levelGauge));
        middle.VerticalAlignment = VAlignment.Center;
        var right = Tgui.HBox(4,
            TguiLayout.LabeledControl(Loc.GetString("hypertorus-ui-energy"), _energyGauge),
            TguiLayout.LabeledControl(Loc.GetString("hypertorus-ui-activity"), _activityGauge),
            TguiLayout.LabeledControl(Loc.GetString("hypertorus-ui-instability"), _instabilityGauge));
        foreach (var group in new[] { left, middle, right })
            group.HorizontalExpand = true;
        _root.AddChild(Tgui.MakeSection(Loc.GetString("hypertorus-ui-parameters"), null, Tgui.HBox(8, left, middle, right)));

        // ── HypertorusSecondaryControls ──
        var controls = Tgui.HBox(8);
        controls.HorizontalAlignment = HAlignment.Center;
        AddComboKnob(controls, HypertorusParameter.HeatingConductor, "hypertorus-ui-heating-conductor", "J/cm", 50, 100, 500, 5,
            "fire", false, "hypertorus-ui-help-heating-conductor");
        AddComboKnob(controls, HypertorusParameter.CoolingVolume, "hypertorus-ui-cooling-volume", "L", 50, 100, 2000, 25,
            "snowflake-o", false, "hypertorus-ui-help-cooling-volume");
        AddComboKnob(controls, HypertorusParameter.MagneticConstrictor, "hypertorus-ui-magnetic-constrictor", "m³/T", 50, 100, 1000, 5,
            "magnet", true, "hypertorus-ui-help-magnetic-constrictor");
        AddComboKnob(controls, HypertorusParameter.CurrentDamper, "hypertorus-ui-current-damper", "W", 0, 0, 1000, 5,
            "sun-o", false, "hypertorus-ui-help-current-damper");
        _root.AddChild(Tgui.MakeSection(Loc.GetString("hypertorus-ui-controls"), null, controls));

        // ── HypertorusWasteRemove ──
        _wasteButton = Tgui.Button(string.Empty, () => Toggle(HypertorusToggle.WasteRemove));
        _filterRate = RateInput(HypertorusParameter.ModeratorFilteringRate, 5, 200);
        var waste = new GridContainer { Columns = 2, HSeparationOverride = 8, VSeparationOverride = 4 };
        waste.AddChild(HelpLabel(Loc.GetString("hypertorus-ui-waste-remove") + ":", Loc.GetString("hypertorus-ui-help-waste")));
        waste.AddChild(Tgui.HBox(0, _wasteButton));
        waste.AddChild(HelpLabel(Loc.GetString("hypertorus-ui-filter-rate") + ":", null));
        waste.AddChild(Tgui.HBox(0, _filterRate));
        waste.AddChild(HelpLabel(Loc.GetString("hypertorus-ui-filter") + ":", null));
        _filters.HorizontalExpand = true;
        waste.AddChild(_filters);
        _root.AddChild(Tgui.MakeSection(Loc.GetString("hypertorus-ui-output"), null, waste));
    }

    private static GridContainer GasGrid() => new() { Columns = 2, HSeparationOverride = 8, VSeparationOverride = 4, HorizontalExpand = true };

    private void Send(BoundUserInterfaceMessage message)
    {
        OnMessage?.Invoke(message);
    }

    private void Toggle(HypertorusToggle toggle)
    {
        Send(new HypertorusToggleMessage(toggle));
    }

    private TguiNumberInput RateInput(HypertorusParameter parameter, float min, float max)
    {
        var input = new TguiNumberInput { MinValue = min, MaxValue = max, Step = 1, Unit = "mol/s" };
        input.OnChange += value => Send(new HypertorusParameterMessage(parameter, Math.Clamp(value, min, max)));
        return input;
    }

    /// <summary>Подпись LabeledList с HoverHelp (вопрос с подсказкой) или HelpDummy (отступ).</summary>
    private static Control HelpLabel(string text, string? help)
    {
        var box = Tgui.HBox(6);
        var icon = TguiIcons.Icon("question-circle", 12, Tgui.Label);
        icon.MinWidth = 12;
        if (help != null)
        {
            icon.ToolTip = help;
            icon.MouseFilter = MouseFilterMode.Stop;
        }
        else
        {
            icon.Modulate = Color.Transparent;
        }

        box.AddChild(icon);
        box.AddChild(Tgui.Text(text, Tgui.Label));
        box.VerticalAlignment = VAlignment.Center;
        return box;
    }

    /// <summary>ComboKnob: иконка слева, ручка size=2 и кнопки максимум / по умолчанию / минимум справа.</summary>
    private void AddComboKnob(BoxContainer row, HypertorusParameter parameter, string label, string unit,
        float min, float def, float max, float step, string icon, bool flipIcon, string help)
    {
        var knob = new TguiKnob(2) { MinValue = min, MaxValue = max, Step = step, StepPixelSize = 1, Unit = unit };
        knob.OnChange += value => Send(new HypertorusParameterMessage(parameter, value));
        _knobs[parameter] = knob;

        var iconLabel = TguiIcons.Icon(icon, 24, Tgui.Label);
        iconLabel.ToolTip = Loc.GetString(help);
        iconLabel.MouseFilter = MouseFilterMode.Stop;
        iconLabel.MinWidth = 30;

        var buttons = Tgui.VBox(0,
            KnobButton("fast-forward", () => Send(new HypertorusParameterMessage(parameter, max))),
            KnobButton("undo", () => Send(new HypertorusParameterMessage(parameter, def))),
            KnobButton("fast-backward", () => Send(new HypertorusParameterMessage(parameter, min))));

        var combo = Tgui.HBox(2, iconLabel, knob, buttons);
        row.AddChild(TguiLayout.LabeledControl(Loc.GetString(label), combo));
    }

    private static TguiButton KnobButton(string icon, Action onPressed)
    {
        var button = Tgui.Button(string.Empty, onPressed, Tgui.Transparent, icon: icon);
        button.MinHeight = 18;
        return button;
    }

    public void UpdateState(HypertorusUiState state)
    {
        _state = state;
        _startPower = state.StartPower;

        SetToggle(_powerButton, state.StartPower);
        _powerButton.Disabled = state.PowerLevel > 0;
        SetToggle(_coolingButton, state.StartCooling);
        _coolingButton.Disabled = state.StartFuel || state.StartModerator || !state.StartPower
                                  || state.StartCooling && state.PowerLevel > 0;

        var injectionDisabled = !state.StartPower || !state.StartCooling;
        SetToggle(_fuelButton, state.StartFuel);
        _fuelButton.Disabled = injectionDisabled;
        SetToggle(_moderatorButton, state.StartModerator);
        _moderatorButton.Disabled = injectionDisabled;
        SetToggle(_wasteButton, state.WasteRemove);

        _fuelRate.Value = state.FuelInjectionRate;
        _moderatorRate.Value = state.ModeratorInjectionRate;
        _filterRate.Value = state.ModeratorFilteringRate;

        SetKnob(HypertorusParameter.HeatingConductor, state.HeatingConductor,
            state.HeatingConductor > 50 && state.HeatOutput > 0 ? Tgui.Yellow : null);
        SetKnob(HypertorusParameter.CoolingVolume, state.CoolingVolume, null);
        SetKnob(HypertorusParameter.MagneticConstrictor, state.MagneticConstrictor, null);
        SetKnob(HypertorusParameter.CurrentDamper, state.CurrentDamper, state.CurrentDamper > 0 ? Tgui.Yellow : null);

        UpdateRecipes(state);
        UpdateGases(state);
        UpdateTemperatures(state);
        UpdateParameters(state);
        UpdateFilters(state);
    }

    private void SetKnob(HypertorusParameter parameter, float value, Color? color)
    {
        var knob = _knobs[parameter];
        knob.Value = value;
        knob.FillColor = color ?? Tgui.PrimaryLine;
    }

    /// <summary>Button icon=power-off/times, content Вкл/Выкл, selected — зелёная.</summary>
    private static void SetToggle(TguiButton button, bool on)
    {
        button.TextLabel.Text = on ? Loc.GetString("hypertorus-ui-on") : Loc.GetString("hypertorus-ui-off");
        button.SetIcon(on ? "power-off" : "times");
        button.Color = on ? Tgui.Green : Tgui.Primary;
    }

    private HypertorusFuelPrototype? SelectedFuel(HypertorusUiState state)
    {
        return state.Selected != null && _proto.TryIndex<HypertorusFuelPrototype>(state.Selected, out var fuel) ? fuel : null;
    }

    private GasPrototype GasProto(Gas gas)
    {
        return _proto.Index<GasPrototype>(gas.ToString());
    }

    private string GasName(Gas gas)
    {
        return Loc.GetString(GasProto(gas).Name);
    }

    /// <summary>getGasLabel: короткое обозначение газа.</summary>
    private string GasLabel(Gas gas)
    {
        return PlainDigits(Loc.GetString(GasProto(gas).Abbreviation));
    }

    private Color GasColor(Gas gas)
    {
        return GasProto(gas).Color;
    }

    #region Рецепты

    private void UpdateRecipes(HypertorusUiState state)
    {
        if (_recipesBuiltFor == state.Selected && _recipesBuiltLevel == (state.PowerLevel == 0 ? 0 : 1))
            return;

        _recipesBuiltFor = state.Selected;
        _recipesBuiltLevel = state.PowerLevel == 0 ? 0 : 1;
        _recipes.RemoveAllChildren();

        var table = new GridContainer { Columns = 17, HSeparationOverride = 6, VSeparationOverride = 3 };

        // Первая строка заголовка (colSpan в tgui): Топливо ×2, Побочные продукты ×2, Газы ×6, Эффекты ×6.
        var groups = new (string? Text, int Span)[]
        {
            (null, 1),
            (Loc.GetString("hypertorus-ui-recipe-fuel"), 2),
            (Loc.GetString("hypertorus-ui-recipe-byproducts"), 2),
            (Loc.GetString("hypertorus-ui-recipe-products"), 6),
            (Loc.GetString("hypertorus-ui-recipe-effects"), 6),
        };
        foreach (var (text, span) in groups)
        {
            table.AddChild(Tgui.Text(text ?? string.Empty, Tgui.TextColor, 12, true));
            for (var i = 1; i < span; i++)
                table.AddChild(new Control());
        }

        // Вторая строка заголовка.
        table.AddChild(new Control());
        table.AddChild(Tgui.Text(Loc.GetString("hypertorus-ui-recipe-primary"), Tgui.TextColor, 12, true));
        table.AddChild(Tgui.Text(Loc.GetString("hypertorus-ui-recipe-secondary"), Tgui.TextColor, 12, true));
        table.AddChild(new Control());
        table.AddChild(new Control());
        for (var tier = 1; tier <= 6; tier++)
            table.AddChild(Tgui.Text($"Tier {tier}", Tgui.TextColor, 12, true));

        foreach (var (label, icons, _, _) in EffectStructure)
        {
            var icon = TguiIcons.Stack(14, Tgui.Label, icons);
            icon.ToolTip = label;
            icon.MouseFilter = MouseFilterMode.Stop;
            table.AddChild(icon);
        }

        foreach (var fuel in _proto.EnumeratePrototypes<HypertorusFuelPrototype>().OrderBy(f => f.Order))
        {
            var active = fuel.ID == state.Selected;
            var select = Tgui.Button(string.Empty, () => Send(new HypertorusSelectFuelMessage(active ? null : fuel.ID)),
                active ? Tgui.Green : null, disabled: state.PowerLevel != 0, icon: active ? "times" : "power-off");
            table.AddChild(select);

            table.AddChild(GasCell(HypertorusFuelPrototype.ResolveGas(fuel.RequirementIds.ElementAtOrDefault(0) ?? string.Empty)));
            table.AddChild(GasCell(HypertorusFuelPrototype.ResolveGas(fuel.RequirementIds.ElementAtOrDefault(1) ?? string.Empty)));
            table.AddChild(GasCell(fuel.PrimaryProduct(0)));
            table.AddChild(GasCell(fuel.PrimaryProduct(1)));
            for (var i = 0; i < 6; i++)
            {
                table.AddChild(GasCell(fuel.SecondaryProduct(i)));
            }

            var values = new[]
            {
                fuel.NegativeTemperatureMultiplier,
                fuel.PositiveTemperatureMultiplier,
                fuel.EnergyConcentrationMultiplier,
                fuel.FuelConsumptionMultiplier,
                fuel.GasProductionMultiplier,
                fuel.TemperatureChangeMultiplier,
            };
            for (var i = 0; i < EffectStructure.Length; i++)
            {
                var (_, _, scale, baseValue) = EffectStructure[i];
                var tooltip = i == 5
                    ? $"Maximum: {(BaseMaxTemperature * values[i]).ToString("0.###############e+0", CultureInfo.InvariantCulture)} K"
                    : $"x{values[i].ToString(CultureInfo.InvariantCulture)}";
                AddEffect(table, values[i], scale, baseValue, tooltip);
            }
        }

        _recipes.AddChild(new ScrollContainer { VScrollEnabled = false, HScrollEnabled = true, MinHeight = 210, Children = { table } });
    }

    private Control GasCell(Gas? maybeGas)
    {
        if (maybeGas is not { } gas)
            return new Control();

        var label = Tgui.Text(GasLabel(gas), GasColor(gas));
        label.ToolTip = GasName(gas);
        label.MouseFilter = MouseFilterMode.Stop;
        return label;
    }

    /// <summary>Нижние индексы (H₂, O₂) в обычные цифры: в шрифте tgui их нет.</summary>
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

    /// <summary>effect_to_icon: angle-double-up, angle-up, minus, angle-down, angle-double-down.</summary>
    private static void AddEffect(GridContainer table, float value, float scale, float baseValue, string tooltip)
    {
        string icon;
        if (value == baseValue)
            icon = "minus";
        else if (value > baseValue)
            icon = value > baseValue * scale ? "angle-double-up" : "angle-up";
        else
            icon = value < baseValue / scale ? "angle-double-down" : "angle-down";

        var label = TguiIcons.Icon(icon, 14);
        label.ToolTip = tooltip;
        label.MouseFilter = MouseFilterMode.Stop;
        table.AddChild(label);
    }

    #endregion

    #region Газы

    private void UpdateGases(HypertorusUiState state)
    {
        var fuel = SelectedFuel(state);
        _fusionNoRecipe.Visible = fuel == null;
        _fusionGases.Visible = fuel != null;

        FillGasList(_fusionGases, state.FusionGases, fuel?.Requirements ?? new List<Gas>(), false, _fuelButton, _fuelRate,
            Loc.GetString("hypertorus-ui-help-fuel-rate"));
        FillGasList(_moderatorGases, state.ModeratorGases, ModeratorSticky, true, _moderatorButton, _moderatorRate,
            Loc.GetString("hypertorus-ui-help-moderator-rate"));
    }

    /// <summary>GasList: строка «Управление впрыском» и полоски газов (масштаб 500 моль).</summary>
    private void FillGasList(GridContainer grid, List<HypertorusGasEntry> raw, IReadOnlyCollection<Gas> sticky, bool moderator,
        TguiButton button, TguiNumberInput rate, string rateHelp)
    {
        // Кнопка и поле ввода переиспользуются, чтобы не сбивать перетаскивание.
        button.Orphan();
        rate.Orphan();
        grid.RemoveAllChildren();

        grid.AddChild(HelpLabel(Loc.GetString("hypertorus-ui-injection"), rateHelp));
        grid.AddChild(Tgui.HBox(4, button, rate));

        var gases = raw.Where(g => g.Amount >= 0.01f).OrderByDescending(g => g.Amount).ToList();
        foreach (var gas in sticky)
        {
            if (gases.All(g => g.Gas != gas))
                gases.Add(new HypertorusGasEntry(gas, 0));
        }

        foreach (var gas in gases)
        {
            string? help = null;
            if (moderator && ModeratorHelp.TryGetValue(gas.Gas, out var helpKey))
                help = Loc.GetString(helpKey);

            grid.AddChild(HelpLabel(GasLabel(gas.Gas) + ":", help));
            var bar = Tgui.ProgressBar(gas.Amount / 500f, GasColor(gas.Gas),
                $"{gas.Amount.ToString("0.00", CultureInfo.InvariantCulture)} moles");
            bar.HorizontalExpand = true;
            grid.AddChild(bar);
        }
    }

    #endregion

    #region Температуры и состояние

    private void UpdateTemperatures(HypertorusUiState state)
    {
        var fuel = SelectedFuel(state);
        _chart.Update(state, BaseMaxTemperature * (fuel?.TemperatureChangeMultiplier ?? 1));
    }

    private void UpdateParameters(HypertorusUiState state)
    {
        var activity = state.HeatOutput / (state.HeatOutput < 0 ? state.HeatOutputMin : state.HeatOutputMax);
        if (double.IsNaN(activity) || double.IsInfinity(activity))
            activity = 0;

        _integrityGauge.Value = state.Integrity;
        _ironGauge.Value = state.IronContent;
        _apcGauge.Value = state.ApcEnergy;
        _levelGauge.Value = state.PowerLevel;
        _energyGauge.Value = (float) Math.Max(0, state.EnergyLevel > 0 ? Math.Log10(state.EnergyLevel) : 0);
        _activityGauge.Value = (float) (activity * 100);
        _instabilityGauge.Value = (float) Math.Max(state.Instability, 0);
    }

    /// <summary>formatSiUnit из tgui-core с минимальной приставкой (minBase1000 = 4 — тера).</summary>
    private static string FormatSiUnit(double value, int minBase1000, string unit)
    {
        string[] symbols = { "q", "r", "y", "z", "a", "f", "p", "n", "μ", "m", "", "k", "M", "G", "T", "P", "E", "Z", "Y", "R", "Q" };
        const int zero = 10;
        var realBase10 = value == 0 ? int.MinValue / 4 : (int) Math.Floor(Math.Log10(Math.Abs(value)));
        var base10 = Math.Max(minBase1000 * 3, realBase10);
        var base1000 = (int) Math.Floor(base10 / 3.0);
        var symbol = symbols[Math.Min(base1000 + zero, symbols.Length - 1)];
        var scaled = value / Math.Pow(10, base1000 * 3);
        var precision = base1000 > minBase1000 ? 2 + base1000 * 3 - base10 : 0;
        var text = scaled.ToString("F" + Math.Clamp(precision, 0, 15), CultureInfo.InvariantCulture);
        return $"{text} {symbol}{unit}".Trim();
    }

    private void UpdateFilters(HypertorusUiState state)
    {
        _filters.RemoveAllChildren();
        foreach (var gas in Enum.GetValues<Gas>())
        {
            var enabled = state.FilteredGases.Contains(gas);
            _filters.AddChild(TguiLayout.Checkbox(GasName(gas), enabled, () => Send(new HypertorusFilterMessage(gas))));
        }
    }

    #endregion

    /// <summary>
    /// HypertorusTemperatures: четыре столбца на общей логарифмической шкале
    /// с отметками предыдущего и следующего уровня синтеза.
    /// </summary>
    private sealed class TemperatureChart : Control
    {
        private const float ChartHeight = 200;
        private readonly (string Label, Color Color)[] _bars =
        {
            ("hypertorus-ui-temp-fusion", FusionColor),
            ("hypertorus-ui-temp-moderator", ModeratorColor),
            ("hypertorus-ui-temp-coolant", CoolantColor),
            ("hypertorus-ui-temp-output", OutputColor),
        };

        private readonly double[] _values = new double[4];
        private readonly double[] _deltas = new double[4];
        private double _min = 2.73, _max = 500, _prevLevel, _nextLevel = 500;

        public TemperatureChart()
        {
            MinSize = new Vector2(380, ChartHeight + 70);
        }

        public void Update(HypertorusUiState state, double maxTemperature)
        {
            var period = Math.Max(state.TemperaturePeriod, 0.01f);
            _values[0] = state.FusionTemperature;
            _values[1] = state.ModeratorTemperature;
            _values[2] = state.CoolantTemperature;
            _values[3] = state.OutputTemperature;
            _deltas[0] = (state.FusionTemperature - state.FusionTemperatureArchived) / period;
            _deltas[1] = (state.ModeratorTemperature - state.ModeratorTemperatureArchived) / period;
            _deltas[2] = (state.CoolantTemperature - state.CoolantTemperatureArchived) / period;
            _deltas[3] = (state.OutputTemperature - state.OutputTemperatureArchived) / period;

            _prevLevel = Math.Pow(10, 1 + state.PowerLevel);
            _nextLevel = Math.Pow(10, 2 + state.PowerLevel);
            switch (state.PowerLevel)
            {
                case 0:
                    _prevLevel = 0;
                    _nextLevel = 500;
                    break;
                case 1:
                    _prevLevel = 500;
                    break;
                case 6:
                    _nextLevel = maxTemperature;
                    break;
            }

            var all = new List<double> { _prevLevel, _nextLevel };
            all.AddRange(_values.Where(v => v > 0));
            _max = all.Max();
            var positive = all.Where(v => v > 0).ToList();
            _min = Math.Max(2.73, Math.Min(20, positive.Count > 0 ? positive.Min() : 20));
            if (state.PowerLevel == 6)
                _nextLevel = 0;
        }

        private float ValueToY(double value)
        {
            if (value <= 0 || _max <= _min)
                return 0;

            var ratio = (Math.Log10(value) - Math.Log10(_min)) / (Math.Log10(_max) - Math.Log10(_min));
            return (float) (ChartHeight * Math.Clamp(ratio, 0, 1));
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            var scale = UIScale;
            var font = Tgui.Font(11);
            var axisX = 70 * scale;
            var top = 8 * scale;
            var bottom = top + ChartHeight * scale;

            // Ось и отметки.
            handle.DrawLine(new Vector2(axisX, top), new Vector2(axisX, bottom), Tgui.Label);
            handle.DrawLine(new Vector2(axisX, bottom), new Vector2(PixelSize.X, bottom), Tgui.Label);
            DrawTick(handle, font, axisX, bottom, _min, null);
            if (_prevLevel > 0)
                DrawTick(handle, font, axisX, bottom, _prevLevel, "chevron-down");
            if (_nextLevel > 0)
                DrawTick(handle, font, axisX, bottom, _nextLevel, "chevron-up");
            DrawTick(handle, font, axisX, bottom, _max, null);

            // Столбцы.
            var width = (PixelSize.X - axisX) / _bars.Length;
            for (var i = 0; i < _bars.Length; i++)
            {
                var x = axisX + width * i + width / 2;
                var barWidth = 22 * scale;
                var y = ValueToY(_values[i]) * scale;
                handle.DrawRect(new UIBox2(x - barWidth / 2, top, x + barWidth / 2, bottom), Color.Black.WithAlpha(0.33f));
                if (_values[i] > 0)
                    handle.DrawRect(new UIBox2(x - barWidth / 2, bottom - y, x + barWidth / 2, bottom), _bars[i].Color);

                var lines = new List<(string, Color)> { (Loc.GetString(_bars[i].Label), Tgui.Label) };
                if (_values[i] > 0)
                {
                    lines.Add(($"{ToExponentialIfBig(_values[i])} K", Tgui.Label));
                    lines.Add((_deltas[i] == 0 ? "-" : $"{(_deltas[i] < 0 ? "" : "+")}{ToExponentialIfBig(_deltas[i])} K/s", Tgui.Label));
                }
                else
                {
                    lines.Add((Loc.GetString("hypertorus-ui-empty"), Tgui.Bad));
                }

                var lineY = bottom + 4 * scale;
                foreach (var (text, color) in lines)
                {
                    var size = handle.GetDimensions(font, text, scale);
                    handle.DrawString(font, new Vector2(x - size.X / 2, lineY), text, scale, color);
                    lineY += size.Y;
                }
            }
        }

        private void DrawTick(DrawingHandleScreen handle, Font font, float axisX, float bottom, double value, string? icon)
        {
            var y = bottom - ValueToY(value) * UIScale;
            handle.DrawLine(new Vector2(axisX - 4 * UIScale, y), new Vector2(axisX, y), Tgui.Label);
            var text = $"{ToExponentialIfBig(value)} K";
            var size = handle.GetDimensions(font, text, UIScale);
            var x = axisX - 6 * UIScale - size.X;
            handle.DrawString(font, new Vector2(x, y - size.Y / 2), text, UIScale, Tgui.Label);
            if (icon != null)
                TguiIcons.DrawGlyph(handle, icon, 10, new Vector2(x - 8 * UIScale, y), Tgui.Label, UIScale);
        }

        /// <summary>to_exponential_if_big: больше 5000 — в экспоненциальной записи.</summary>
        private static string ToExponentialIfBig(double value)
        {
            return Math.Abs(value) > 5000
                ? value.ToString("0.0e+0", CultureInfo.InvariantCulture)
                : Math.Round(value).ToString(CultureInfo.InvariantCulture);
        }
    }
}
