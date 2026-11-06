using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Fishing;

/// <summary>Окно энциклопедии рыб (tgui FishCatalog): список рыб слева, сведения справа.</summary>
public sealed class ImperialFishCatalogBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private BoxContainer? _list;
    private BoxContainer? _details;
    private Label? _title;
    private List<ImperialFishCatalogEntry> _entries = new();
    private ImperialFishCatalogEntry? _current;
    private readonly Dictionary<ImperialFishCatalogEntry, Button> _buttons = new();

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.SetSize = new Vector2(560, 360);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };

        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var listScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        listScroll.AddChild(_list);
        var listSection = ImperialFishingUi.Section(null, listScroll);
        listSection.SetWidth = 160;
        listSection.MinWidth = 160;
        root.AddChild(listSection);

        var right = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _title = new Label { StyleClasses = { "LabelHeading" } };
        _details = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var detailScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        detailScroll.AddChild(_details);
        right.AddChild(detailScroll);

        var titled = ImperialFishingUi.Section(Loc.GetString("imperial-fish-catalog-index"), right);
        titled.HorizontalExpand = true;
        // Заголовок секции меняется на имя выбранной рыбы.
        _title = FindHeading(titled);
        root.AddChild(titled);

        _window.Contents.AddChild(root);
    }

    private static Label? FindHeading(Control control)
    {
        foreach (var child in control.Children)
        {
            if (child is Label { StyleClasses.Count: > 0 } label && label.StyleClasses.Contains("LabelHeading"))
                return label;
            if (FindHeading(child) is { } found)
                return found;
        }

        return null;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialFishCatalogBoundUserInterfaceState catalog || _list == null)
            return;

        _entries = catalog.Entries.OrderBy(e => e.Name).ToList();
        _list.RemoveAllChildren();
        _buttons.Clear();
        foreach (var entry in _entries)
        {
            var button = new Button
            {
                Text = entry.Name,
                ClipText = true,
                HorizontalExpand = true,
                ToggleMode = true,
                StyleClasses = { "ButtonSquare" },
            };
            var captured = entry;
            button.OnPressed += _ => Select(captured);
            _list.AddChild(button);
            _buttons[entry] = button;
        }
    }

    private void Select(ImperialFishCatalogEntry fish)
    {
        _current = fish;
        foreach (var (entry, button) in _buttons)
            button.Pressed = entry == fish;

        if (_details == null)
            return;

        if (_title != null)
            _title.Text = fish.Name.Length > 0 ? fish.Name[..1].ToUpperInvariant() + fish.Name[1..] : fish.Name;

        _details.RemoveAllChildren();
        Item("imperial-fish-catalog-ui-description", fish.Description);
        Item("imperial-fish-catalog-ui-fluid", fish.Fluid);
        Item("imperial-fish-catalog-ui-temperature", Loc.GetString("imperial-fish-catalog-ui-temperature-value", ("min", fish.TempMin), ("max", fish.TempMax)));
        Item("imperial-fish-catalog-ui-feeding", fish.Feed);
        Item("imperial-fish-catalog-ui-acquisition", fish.Source);
        Item("imperial-fish-catalog-ui-size", Loc.GetString("imperial-fish-catalog-ui-size-value", ("size", fish.Size)));
        Item("imperial-fish-catalog-ui-weight", Loc.GetString("imperial-fish-catalog-ui-weight-value", ("weight", fish.Weight)));
        Item("imperial-fish-catalog-ui-beauty", fish.Beauty.ToString());

        var tips = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        tips.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-fish-catalog-ui-spots"), ImperialFishingUi.Text(fish.Tips.Spots), 110));
        tips.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-fish-catalog-ui-favorite"), ImperialFishingUi.Text(fish.Tips.FavoriteBait), 110));
        tips.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-fish-catalog-ui-disliked"), ImperialFishingUi.Text(fish.Tips.DislikedBait), 110));
        tips.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-fish-catalog-ui-behavior"), ImperialFishingUi.Text(string.Join(" ", fish.Tips.Traits)), 110));
        _details.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-fish-catalog-ui-tips"), tips, 110));

        var icon = ImperialFishingUi.Icon(EntMan.System<SpriteSystem>(), fish.Prototype, 64);
        icon.HorizontalAlignment = Control.HAlignment.Left;
        _details.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-fish-catalog-ui-illustration"), icon, 110));
    }

    private void Item(string label, string value)
    {
        _details?.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString(label), ImperialFishingUi.Text(value), 110));
    }
}
