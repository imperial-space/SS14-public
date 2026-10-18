using System.Linq;
using Content.Client.Imperial.Fishing;
using Content.Shared.Imperial.Cargo;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Cargo;

/// <summary>tgui CargoCatalog: категории слева, наборы справа, поиск по названию.</summary>
public sealed class ImperialCargoCatalog : BoxContainer
{
    private readonly LineEdit _search;
    private readonly BoxContainer _categories;
    private readonly BoxContainer _packs;
    private readonly List<(Button Button, int Cost)> _buyButtons = new();

    private List<ImperialExpressPack> _list = new();
    private string _signature = string.Empty;
    private string _category = string.Empty;
    private int _points;

    public event Action<string>? OnBuy;

    public ImperialCargoCatalog()
    {
        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = true;

        var body = new BoxContainer { Orientation = LayoutOrientation.Horizontal, VerticalExpand = true };
        var left = new BoxContainer { Orientation = LayoutOrientation.Vertical, MinWidth = 180 };
        _search = new LineEdit { PlaceHolder = Loc.GetString("imperial-express-ui-search") };
        _search.OnTextChanged += _ => FillPacks();
        left.AddChild(_search);
        _categories = new BoxContainer { Orientation = LayoutOrientation.Vertical };
        var catScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        catScroll.AddChild(_categories);
        left.AddChild(catScroll);
        body.AddChild(left);

        _packs = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, Margin = new Thickness(6, 0, 0, 0) };
        var packScroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false };
        packScroll.AddChild(_packs);
        body.AddChild(packScroll);

        var section = ImperialFishingUi.Section(Loc.GetString("imperial-express-ui-catalog"), body);
        section.VerticalExpand = true;
        AddChild(section);
    }

    /// <summary>Каталог пересобирается только при смене набора или цен, кнопки покупки — по балансу.</summary>
    public void Update(List<ImperialExpressPack> packs, int points)
    {
        _points = points;
        var signature = string.Join(";", packs.Select(p => $"{p.Id}:{p.Cost}"));
        if (signature != _signature)
        {
            _signature = signature;
            _list = packs;
            FillCategories();
            FillPacks();
        }

        foreach (var (button, cost) in _buyButtons)
        {
            button.Disabled = cost > points;
        }
    }

    private void FillCategories()
    {
        _categories.RemoveAllChildren();
        var categories = _list.Select(p => p.Category).Distinct().ToList();
        if (!categories.Contains(_category))
            _category = categories.FirstOrDefault() ?? string.Empty;

        foreach (var category in categories)
        {
            var button = new Button
            {
                Text = $"{category} ({_list.Count(p => p.Category == category)})",
                ToggleMode = true,
                Pressed = category == _category,
                ClipText = true,
            };
            var chosen = category;
            button.OnPressed += _ =>
            {
                _category = chosen;
                _search.Text = string.Empty;
                foreach (var child in _categories.Children)
                {
                    if (child is Button other)
                        other.Pressed = other == button;
                }

                FillPacks();
            };
            _categories.AddChild(button);
        }
    }

    private void FillPacks()
    {
        _packs.RemoveAllChildren();
        _buyButtons.Clear();

        var search = _search.Text.Trim();
        var packs = search != string.Empty
            ? _list.Where(p => p.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            : _list.Where(p => p.Category == _category);

        foreach (var pack in packs)
        {
            var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(0, 1) };
            row.AddChild(new Label { Text = pack.Name, HorizontalExpand = true, ClipText = true, ToolTip = pack.Description });
            var buy = new Button
            {
                Text = Loc.GetString("imperial-express-ui-cost", ("cost", pack.Cost)),
                MinWidth = 90,
                Disabled = pack.Cost > _points,
                ToolTip = pack.Description,
            };
            var id = pack.Id;
            buy.OnPressed += _ => OnBuy?.Invoke(id);
            _buyButtons.Add((buy, pack.Cost));
            row.AddChild(buy);
            _packs.AddChild(row);
        }
    }
}
