using System.Numerics;
using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Fishing;

/// <summary>
/// Окно анализатора рыб (tgui FishAnalyzer): фиолетовые карточки рыб со здоровьем, сытостью,
/// кормом, чертами (подсказки), эволюциями, температурой и средой. Для аквариума — поиск и сетка.
/// </summary>
public sealed class ImperialFishAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private static readonly Color WindowBackground = Color.FromHex("#402784");
    private static readonly Color CardBackground = Color.FromHex("#3a1679");
    private static readonly Color CardBorder = Color.FromHex("#6f1d94");
    private static readonly Color IconBackground = Color.FromHex("#1a0940");
    private static readonly Color Good = Color.FromHex("#1ac400");

    private DefaultWindow? _window;
    private LineEdit? _search;
    private GridContainer? _grid;
    private ImperialFishAnalyzerBoundUserInterfaceState? _state;
    private bool? _single;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-fish-analyzer-title");
        _window.SetSize = new Vector2(530, 270);
    }

    private void Build(bool single)
    {
        if (_window == null)
            return;

        _single = single;
        _window.Contents.RemoveAllChildren();
        _window.SetSize = single ? new Vector2(530, 290) : new Vector2(700, 480);

        var background = new PanelContainer { PanelOverride = new StyleBoxFlat { BackgroundColor = WindowBackground } };
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(4) };
        if (!single)
        {
            _search = new LineEdit { PlaceHolder = Loc.GetString("imperial-fish-analyzer-search"), HorizontalExpand = true };
            _search.OnTextChanged += _ => Rebuild();
            root.AddChild(ImperialFishingUi.Section(null, _search));
        }

        _grid = new GridContainer { Columns = single ? 1 : 2, HorizontalExpand = true };
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_grid);
        var section = ImperialFishingUi.Section(Loc.GetString("imperial-fish-analyzer-fish"), scroll);
        section.VerticalExpand = true;
        root.AddChild(section);
        background.AddChild(root);
        _window.Contents.AddChild(background);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialFishAnalyzerBoundUserInterfaceState analyzer)
            return;

        _state = analyzer;
        if (_single != analyzer.FishScanned)
            Build(analyzer.FishScanned);

        Rebuild();
    }

    private void Rebuild()
    {
        if (_grid == null || _state == null)
            return;

        _grid.RemoveAllChildren();
        var filter = _search?.Text.Trim() ?? string.Empty;
        foreach (var fish in _state.Entries)
        {
            if (filter.Length > 0 && !fish.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            _grid.AddChild(FishCard(fish));
        }
    }

    /// <summary>dissectName(): первое слово, не длиннее 12 символов.</summary>
    private static string Dissect(string name, int length = 12)
    {
        var first = name.Split(' ')[0];
        return first.Length > length ? first[..length] : first;
    }

    private Control FishCard(ImperialFishAnalyzerEntry fish)
    {
        var sprite = EntMan.System<SpriteSystem>();
        var card = ImperialFishingUi.Card(CardBackground, CardBorder);
        card.HorizontalExpand = true;
        card.MinHeight = 120;

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, MinHeight = 105 };

        // Иконка, вес и размер.
        var left = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 54 };
        left.AddChild(ImperialFishingUi.Icon(sprite, fish.Proto, 40, IconBackground));
        left.AddChild(Small(Loc.GetString("imperial-fish-analyzer-weight", ("weight", fish.Weight))));
        left.AddChild(Small(Loc.GetString("imperial-fish-analyzer-size", ("size", fish.Size))));
        top.AddChild(left);

        // Имя, корм, черты.
        var middle = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, Margin = new Thickness(4, 0) };
        middle.AddChild(new Label { Text = Dissect(fish.Name).ToUpperInvariant(), StyleClasses = { "LabelHeading" }, FontColorOverride = Color.White });

        var food = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = fish.FoodColor },
            Margin = new Thickness(4, 2),
        };
        food.AddChild(new Label { Text = fish.Food, FontColorOverride = Color.White, Align = Label.AlignMode.Center });
        middle.AddChild(food);

        var traits = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(0, 6, 0, 0) };
        if (fish.Traits.Count == 0)
        {
            traits.AddChild(new Button { Text = Loc.GetString("imperial-fish-analyzer-no-traits"), ToolTip = Loc.GetString("imperial-fish-analyzer-no-traits-tip"), StyleClasses = { "ButtonSquare" } });
        }
        else
        {
            foreach (var trait in fish.Traits)
            {
                traits.AddChild(new Button
                {
                    Text = trait.Name,
                    ToolTip = Loc.GetString("imperial-fish-analyzer-inherit", ("inherit", trait.Inheritability)) + "\n" + trait.Description,
                    StyleClasses = { "ButtonSquare" },
                });
            }
        }

        middle.AddChild(traits);
        top.AddChild(middle);

        // Здоровье, сытость, эволюции.
        var right = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, Margin = new Thickness(8, 0, 0, 0) };
        right.AddChild(new Label { Text = Loc.GetString("imperial-fish-analyzer-health") });
        right.AddChild(Bar(fish.Health / 100f));
        right.AddChild(new Label { Text = Loc.GetString("imperial-fish-analyzer-hunger") });
        right.AddChild(Bar(fish.Hunger));

        var evolutions = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(0, 6, 0, 0) };
        if (fish.Evolutions.Count == 0)
            evolutions.AddChild(new Label { Text = Loc.GetString("imperial-fish-analyzer-no-evolutions") });

        foreach (var evolution in fish.Evolutions)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, ToolTip = evolution.Name + (evolution.Conditions.Length > 0 ? "\n" + evolution.Conditions : string.Empty) };
            row.AddChild(ImperialFishingUi.Icon(sprite, fish.Proto, 32));
            var arrow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(2, 0) };
            arrow.AddChild(Small($"{evolution.Probability}%"));
            arrow.AddChild(new Label { Text = "→", FontColorOverride = Color.Red, Align = Label.AlignMode.Center });
            row.AddChild(arrow);
            row.AddChild(ImperialFishingUi.Icon(sprite, evolution.Proto, 32));
            evolutions.AddChild(row);
        }

        right.AddChild(evolutions);
        top.AddChild(right);
        root.AddChild(top);

        // Температура и среда.
        var bottom = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        bottom.AddChild(new Label
        {
            Text = $"{fish.MinTemp}K - {fish.MaxTemp}K",
            FontColorOverride = fish.SuitableTemp ? Good : Color.Red,
            HorizontalExpand = true,
        });
        bottom.AddChild(new Label
        {
            Text = Dissect(fish.Fluid, 18),
            FontColorOverride = fish.FluidCompatible ? Good : Color.Red,
            HorizontalExpand = true,
            Margin = new Thickness(12, 0, 0, 0),
        });
        root.AddChild(bottom);
        card.AddChild(root);
        return card;
    }

    private static Label Small(string text)
    {
        return new Label { Text = text, StyleClasses = { "LabelSmall" }, FontColorOverride = Color.White };
    }

    /// <summary>ProgressBar с диапазонами good ≥ 0.9, average 0.5–0.9, bad &lt; 0.5.</summary>
    private static ProgressBar Bar(float value)
    {
        var color = value >= 0.9f ? Color.FromHex("#5baa27") : value >= 0.5f ? Color.FromHex("#d09000") : Color.FromHex("#bd2020");
        return new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = Math.Clamp(value, 0, 1),
            MinHeight = 14,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 8, 2),
            BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1a0940") },
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = color },
        };
    }
}
