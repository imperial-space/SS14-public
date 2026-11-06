using System.Numerics;
using Content.Shared.Imperial.Fishing;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Fishing;

/// <summary>Выбор черты для извлечения (tgui_input_list «Fish Trait Extraction»).</summary>
public sealed class ImperialFishGenegunBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private BoxContainer? _list;
    private LineEdit? _search;
    private ImperialFishGenegunBoundUserInterfaceState? _state;
    private string? _selected;
    private readonly Dictionary<string, Button> _buttons = new();

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-fish-genegun-ui-title");
        _window.SetSize = new Vector2(325, 355);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(4) };
        root.AddChild(new Label { Text = Loc.GetString("imperial-fish-genegun-ui-message"), Margin = new Thickness(0, 0, 0, 4) });

        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_list);
        var section = ImperialFishingUi.Section(null, scroll);
        section.VerticalExpand = true;
        root.AddChild(section);

        _search = new LineEdit { PlaceHolder = Loc.GetString("imperial-fish-genegun-ui-search"), HorizontalExpand = true };
        _search.OnTextChanged += _ => Rebuild();
        root.AddChild(_search);

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var confirm = new Button { Text = Loc.GetString("imperial-fish-genegun-ui-confirm"), HorizontalExpand = true, StyleClasses = { "ButtonColorGreen" } };
        confirm.OnPressed += _ => Confirm();
        var cancel = new Button { Text = Loc.GetString("imperial-fish-genegun-ui-cancel"), HorizontalExpand = true, StyleClasses = { "ButtonColorRed" } };
        cancel.OnPressed += _ => Close();
        buttons.AddChild(confirm);
        buttons.AddChild(cancel);
        root.AddChild(buttons);
        _window.Contents.AddChild(root);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialFishGenegunBoundUserInterfaceState genegun)
            return;

        _state = genegun;
        _selected = genegun.Traits.Count > 0 ? genegun.Traits[0].Id : null;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_list == null || _state == null)
            return;

        _list.RemoveAllChildren();
        _buttons.Clear();
        var filter = _search?.Text.Trim() ?? string.Empty;
        foreach (var trait in _state.Traits)
        {
            if (filter.Length > 0 && !trait.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var button = new Button
            {
                Text = trait.Name,
                ToggleMode = true,
                Pressed = trait.Id == _selected,
                HorizontalExpand = true,
                StyleClasses = { "ButtonSquare" },
            };
            var id = trait.Id;
            button.OnPressed += _ =>
            {
                _selected = id;
                foreach (var (other, b) in _buttons)
                    b.Pressed = other == id;
            };
            _list.AddChild(button);
            _buttons[id] = button;
        }
    }

    private void Confirm()
    {
        if (_state == null || _selected == null)
            return;

        SendMessage(new ImperialFishGenegunExtractMessage(_state.Fish, _selected));
        Close();
    }
}
