using System.Linq;
using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Kitchen;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Kitchen;

/// <summary>Открывает меню кулинарии и обменивается с сервером списком доступного.</summary>
public sealed class ImperialCraftMenuSystem : EntitySystem
{
    private ImperialCraftMenu? _window;

    public HashSet<string> Craftable = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialCraftMenuActionEvent>(OnAction);
        SubscribeNetworkEvent<ImperialCraftStateMessage>(OnState);
    }

    private void OnAction(ImperialCraftMenuActionEvent args)
    {
        args.Handled = true;
        if (_window != null)
        {
            _window.Close();
            return;
        }

        _window = new ImperialCraftMenu(this);
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
        Refresh();
    }

    public void Refresh() => RaiseNetworkEvent(new ImperialCraftStateRequest());

    public void Craft(string recipe) => RaiseNetworkEvent(new ImperialCraftMessage(recipe));

    private void OnState(ImperialCraftStateMessage msg)
    {
        Craftable = msg.Craftable;
        _window?.Rebuild();
    }
}

/// <summary>Меню крафта TG, вкладка «Еда»: категории блюд, поиск, состав и кнопка сборки.</summary>
public sealed class ImperialCraftMenu : DefaultWindow
{
    private readonly ImperialCraftMenuSystem _system;
    private readonly IPrototypeManager _proto;

    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private readonly OptionButton _category = new();
    private readonly CheckBox _onlyCraftable = new() { Text = Loc.GetString("imperial-craft-only-available") };
    private readonly BoxContainer _list = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly List<string> _categories = new();

    public ImperialCraftMenu(ImperialCraftMenuSystem system)
    {
        _system = system;
        _proto = IoCManager.Resolve<IPrototypeManager>();

        Title = Loc.GetString("imperial-craft-title");
        MinSize = new Vector2(460, 400);
        SetSize = new Vector2(620, 680);

        _search.PlaceHolder = Loc.GetString("imperial-craft-search");
        _search.OnTextChanged += _ => Rebuild();
        _onlyCraftable.OnToggled += _ => Rebuild();

        _categories.Add(string.Empty);
        _category.AddItem(Loc.GetString("imperial-craft-category-all"), 0);
        foreach (var category in _proto.EnumeratePrototypes<ImperialCraftRecipePrototype>().Select(r => r.Category).Distinct().OrderBy(CategoryName))
        {
            _categories.Add(category);
            _category.AddItem(CategoryName(category), _categories.Count - 1);
        }

        _category.OnItemSelected += args =>
        {
            _category.SelectId(args.Id);
            Rebuild();
        };

        var refresh = new Button { Text = "⟳", ToolTip = Loc.GetString("imperial-craft-refresh") };
        refresh.OnPressed += _ => _system.Refresh();

        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        top.AddChild(_search);
        top.AddChild(_category);
        top.AddChild(refresh);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_list);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        root.AddChild(top);
        root.AddChild(_onlyCraftable);
        root.AddChild(scroll);
        Contents.AddChild(root);
        Rebuild();
    }

    /// <summary>Абстрактный родитель (любое мясо и т.п.) подписывается отдельной строкой.</summary>
    private string IngredientName(EntProtoId id) =>
        Loc.TryGetString($"imperial-craft-any-{id}", out var name) ? name : _proto.Index(id).Name;

    private static string CategoryName(string category) =>
        Loc.TryGetString($"imperial-craft-category-{category.ToLowerInvariant()}", out var name) ? name : category;

    public void Rebuild()
    {
        _list.RemoveAllChildren();
        var search = _search.Text.Trim();
        var category = _categories[_category.SelectedId];

        var recipes = _proto.EnumeratePrototypes<ImperialCraftRecipePrototype>()
            .Where(r => category.Length == 0 || r.Category == category)
            .Where(r => !_onlyCraftable.Pressed || _system.Craftable.Contains(r.ID))
            .Select(r => (Recipe: r, Name: _proto.Index(r.Result).Name))
            .Where(r => search.Length == 0 || r.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(r => _system.Craftable.Contains(r.Recipe.ID))
            .ThenBy(r => r.Name)
            .Take(200);

        foreach (var (recipe, name) in recipes)
            _list.AddChild(Row(recipe, name));
    }

    private Control Row(ImperialCraftRecipePrototype recipe, string name)
    {
        var view = new EntityPrototypeView { SetSize = new Vector2(32, 32), Stretch = SpriteView.StretchMode.Fill };
        view.SetPrototype(recipe.Result);

        var parts = recipe.Ingredients.Select(i => $"{IngredientName(i.Key)} ×{i.Value}")
            .Concat(recipe.Reagents.Select(r => $"{(_proto.TryIndex<ReagentPrototype>(r.Key, out var reagent) ? reagent.LocalizedName : r.Key.Id)} {r.Value}u"));

        var info = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        info.AddChild(new Label { Text = recipe.Amount > 1 ? $"{name} ×{recipe.Amount}" : name, StyleClasses = { "LabelHeading" } });
        info.AddChild(new RichTextLabel { Text = string.Join(", ", parts), HorizontalExpand = true });

        var craftable = _system.Craftable.Contains(recipe.ID);
        var craft = new Button { Text = Loc.GetString("imperial-craft-make"), Disabled = !craftable, VerticalAlignment = VAlignment.Center };
        craft.OnPressed += _ => _system.Craft(recipe.ID);

        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        row.AddChild(view);
        row.AddChild(info);
        row.AddChild(craft);
        return row;
    }
}
