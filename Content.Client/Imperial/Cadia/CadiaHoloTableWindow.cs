using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.Imperial.Cadia;

/// <summary>Стол командования: этажи и слои сверху, инструменты и палитра, голокарта на всё остальное.</summary>
public sealed class CadiaHoloTableWindow : DefaultWindow
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public event Action<BoundUserInterfaceMessage>? OnSend;

    public static readonly Color[] Palette =
    {
        Color.FromHex("#ff3a2a"), Color.FromHex("#ffd37a"), Color.FromHex("#5aff78"), Color.FromHex("#4ab8ff"),
        Color.FromHex("#c66bff"), Color.FromHex("#ffffff"), Color.FromHex("#ff9a3c"), Color.FromHex("#3dffc8"),
    };

    private readonly CadiaHoloTableMapControl _map = new();
    private readonly BoxContainer _levels = new() { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
    private readonly BoxContainer _layers = new() { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
    private readonly BoxContainer _tools = new() { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
    private readonly BoxContainer _options = new() { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
    private readonly LineEdit _text = new() { MinWidth = 220 };
    private readonly Button _clear = new();
    private readonly Label _hint = new() { FontColorOverride = Color.FromHex("#9a9a8a") };

    private CadiaHoloTableState? _state;
    private string _levelsKey = string.Empty;
    private BoxContainer _row2 = default!;
    private BoxContainer _row3 = default!;

    public CadiaHoloTableWindow()
    {
        IoCManager.InjectDependencies(this);
        MinSize = SetSize = new Vector2(1100, 760);

        _map.HorizontalExpand = true;
        _map.VerticalExpand = true;
        _map.OnAdd += element =>
        {
            if (element.Shape == CadiaHoloShape.Text)
            {
                if (string.IsNullOrWhiteSpace(_text.Text))
                    return;
                element.Text = _text.Text.Trim();
            }
            OnSend?.Invoke(new CadiaHoloAddMessage(element));
        };
        _map.OnErase += id => OnSend?.Invoke(new CadiaHoloEraseMessage(id));
        _map.OnOrder += (point, level) =>
        {
            if (_map.OrderSquad > 0)
                OnSend?.Invoke(new CadiaHoloOrderMessage(_map.OrderSquad, _map.OrderType, point, level));
        };

        _text.PlaceHolder = Loc.GetString("cadia-holo-ui-text-placeholder");
        _clear.Text = Loc.GetString("cadia-holo-ui-clear");
        _clear.StyleClasses.Add("Caution");
        _clear.OnPressed += _ => OnSend?.Invoke(new CadiaHoloClearMessage(_map.ActiveLayer));
        var undo = new Button { Text = Loc.GetString("cadia-holo-ui-undo") };
        undo.OnPressed += _ => OnSend?.Invoke(new CadiaHoloUndoMessage());

        BuildLayers();
        BuildTools();

        var palette = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 3 };
        var swatches = new List<Button>();
        foreach (var color in Palette)
        {
            var swatch = new Button { MinSize = new Vector2(22, 22), ToggleMode = true, Pressed = color == _map.DrawColor };
            swatch.ModulateSelfOverride = color;
            swatches.Add(swatch);
            swatch.OnPressed += _ =>
            {
                _map.DrawColor = color;
                foreach (var other in swatches)
                    other.Pressed = other == swatch;
            };
            palette.AddChild(swatch);
        }

        var row1 = Row(new Label { Text = Loc.GetString("cadia-holo-ui-level"), FontColorOverride = Color.FromHex("#e8c66a") }, _levels,
            new Control { MinWidth = 16 }, new Label { Text = Loc.GetString("cadia-holo-ui-layers"), FontColorOverride = Color.FromHex("#e8c66a") }, _layers);
        _row2 = Row(_tools, new Control { MinWidth = 16 }, palette);
        _row3 = Row(_options, _text, new Control { HorizontalExpand = true }, undo, _clear);

        var frame = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#030805"), BorderColor = Color.FromHex("#3d6b45"), BorderThickness = new Thickness(2) },
        };
        frame.AddChild(_map);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, Margin = new Thickness(6) };
        root.AddChild(row1);
        root.AddChild(_row2);
        root.AddChild(_row3);
        root.AddChild(frame);
        root.AddChild(_hint);
        Contents.AddChild(root);
        UpdateHint();
    }

    private static BoxContainer Row(params Control[] children)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        foreach (var child in children)
            row.AddChild(child);
        return row;
    }

    private void BuildLayers()
    {
        _layers.RemoveAllChildren();
        foreach (var layer in Enum.GetValues<CadiaHoloLayer>())
        {
            var key = layer.ToString().ToLowerInvariant();
            var visible = new Button { ToggleMode = true, Pressed = _map.VisibleLayers.Contains(layer), Text = "👁", ToolTip = Loc.GetString("cadia-holo-ui-layer-visible") };
            var active = new Button { ToggleMode = true, Pressed = _map.ActiveLayer == layer, Text = Loc.GetString($"cadia-holo-layer-{key}") };
            visible.OnToggled += args =>
            {
                if (args.Pressed)
                    _map.VisibleLayers.Add(layer);
                else
                    _map.VisibleLayers.Remove(layer);
            };
            active.OnPressed += _ =>
            {
                _map.ActiveLayer = layer;
                _map.VisibleLayers.Add(layer);
                BuildLayers();
            };
            _layers.AddChild(visible);
            _layers.AddChild(active);
        }
    }

    private void BuildTools()
    {
        _tools.RemoveAllChildren();
        foreach (var tool in Enum.GetValues<CadiaHoloTool>())
        {
            if (tool == CadiaHoloTool.Blueprint && _state is not { Planner: true })
                continue;
            var button = new Button { ToggleMode = true, Pressed = _map.Tool == tool, Text = Loc.GetString($"cadia-holo-tool-{tool.ToString().ToLowerInvariant()}") };
            button.OnPressed += _ =>
            {
                _map.Tool = tool;
                BuildTools();
                BuildOptions();
                UpdateHint();
            };
            _tools.AddChild(button);
        }
        BuildOptions();
    }

    /// <summary>Под инструментом — его выбор: тип значка или тип чертежа.</summary>
    private void BuildOptions()
    {
        _options.RemoveAllChildren();
        _text.Visible = _map.Tool == CadiaHoloTool.Text;
        if (_map.Tool == CadiaHoloTool.Marker)
        {
            foreach (var type in Enum.GetValues<CadiaHoloMarkerType>())
            {
                var button = new Button { ToggleMode = true, Pressed = _map.MarkerType == type, Text = Loc.GetString($"cadia-holo-marker-{type.ToString().ToLowerInvariant()}") };
                button.OnPressed += _ =>
                {
                    _map.MarkerType = type;
                    BuildOptions();
                };
                _options.AddChild(button);
            }
        }
        else if (_map.Tool == CadiaHoloTool.Order)
        {
            if (_state == null || _state.Squads.Count == 0)
            {
                _options.AddChild(new Label { Text = Loc.GetString("cadia-holo-no-squads"), FontColorOverride = Color.FromHex("#9a9a8a") });
                return;
            }
            if (_state.Squads.All(s => s.Number != _map.OrderSquad))
                _map.OrderSquad = _state.Squads[0].Number;
            foreach (var squad in _state.Squads)
            {
                var number = squad.Number;
                var button = new Button { ToggleMode = true, Pressed = _map.OrderSquad == number, Text = $"{number}. {squad.Name}" };
                button.ModulateSelfOverride = squad.Color;
                button.OnPressed += _ =>
                {
                    _map.OrderSquad = number;
                    BuildOptions();
                };
                _options.AddChild(button);
            }
            _options.AddChild(new Control { MinWidth = 10 });
            foreach (var order in new[] { CadiaSquadOrder.Hold, CadiaSquadOrder.Advance, CadiaSquadOrder.Retreat })
            {
                var o = order;
                var button = new Button { ToggleMode = true, Pressed = _map.OrderType == order, Text = Loc.GetString($"cadia-squad-order-{order.ToString().ToLowerInvariant()}") };
                button.OnPressed += _ =>
                {
                    _map.OrderType = o;
                    BuildOptions();
                };
                _options.AddChild(button);
            }
        }
        else if (_map.Tool == CadiaHoloTool.Blueprint)
        {
            foreach (var kind in Enum.GetValues<CadiaBlueprintKind>())
            {
                if (_state != null && !CadiaBlueprintRules.Allowed(kind, _state.Faction))
                    continue;
                var button = new Button { ToggleMode = true, Pressed = _map.BlueprintKind == kind, Text = Loc.GetString($"cadia-blueprint-{kind.ToString().ToLowerInvariant()}") };
                button.OnPressed += _ =>
                {
                    _map.BlueprintKind = kind;
                    BuildOptions();
                };
                _options.AddChild(button);
            }
        }
    }

    private void UpdateHint()
    {
        _hint.Text = Loc.GetString($"cadia-holo-hint-{_map.Tool.ToString().ToLowerInvariant()}") + "   " + Loc.GetString("cadia-holo-hint-nav");
    }

    public void Update(CadiaHoloTableState state)
    {
        var plannerChanged = _state?.Planner != state.Planner;
        var squadsChanged = _state == null || !_state.Squads.SequenceEqual(state.Squads);
        _state = state;
        _map.SetState(state);
        if (plannerChanged)
            BuildTools();
        else if (squadsChanged && _map.Tool == CadiaHoloTool.Order)
            BuildOptions();

        // Визор — только просмотр: панели правки скрыты, ЛКМ двигает карту.
        _map.ReadOnly = state.ReadOnly;
        _row2.Visible = !state.ReadOnly;
        _row3.Visible = !state.ReadOnly;
        if (state.ReadOnly)
            _hint.Text = Loc.GetString("cadia-holo-hint-readonly");

        // Очистка слоя — только командирам.
        var commander = _player.LocalEntity is { } local && _entMan.TryGetComponent<CadiaFactionMemberComponent>(local, out var member) &&
                        member.Role is { } role && _proto.TryIndex(role, out var proto) && proto.Commander;
        _clear.Visible = commander;

        var key = string.Join(",", state.Levels);
        if (key == _levelsKey)
            return;
        _levelsKey = key;
        _levels.RemoveAllChildren();
        foreach (var level in state.Levels)
        {
            var button = new Button { ToggleMode = true, Pressed = _map.Level == level, Text = Loc.GetString($"cadia-holo-level-{level.ToString().ToLowerInvariant()}") };
            button.OnPressed += _ =>
            {
                _map.Level = level;
                OnSend?.Invoke(new CadiaHoloRequestTilesMessage(level));
                _levelsKey = string.Empty;
                Update(_state!);
            };
            _levels.AddChild(button);
        }
    }

    public void SetTiles(CadiaHoloTilesMessage tiles)
    {
        _map.SetTiles(tiles);
    }
}
