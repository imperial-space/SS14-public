using System.Linq;
using System.Numerics;
using Content.Client.Imperial.Fishing;
using Content.Shared.Imperial.Manufacturing;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Manufacturing;

/// <summary>tgui_input_list(): выбор чертежа, рецепта или фильтра с поиском.</summary>
public sealed class ImperialManufacturingSelectBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private LineEdit _search = default!;
    private BoxContainer _list = default!;
    private ImperialManufacturingSelectState? _state;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.SetSize = new Vector2(325, 355);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        _search = new LineEdit { PlaceHolder = Loc.GetString("imperial-manufacturing-ui-search"), HorizontalExpand = true };
        _search.OnTextChanged += _ => Rebuild();
        root.AddChild(_search);

        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, Margin = new Thickness(0, 4, 0, 0) };
        scroll.AddChild(_list);
        root.AddChild(scroll);

        _window.Contents.AddChild(root);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialManufacturingSelectState select || _window == null)
            return;

        _state = select;
        _window.Title = select.Title;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_state == null)
            return;

        _list.RemoveAllChildren();
        var filter = _search.Text.Trim();
        foreach (var option in _state.Options)
        {
            if (filter != string.Empty && !option.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                continue;

            var button = new Button { Text = option.Name, HorizontalExpand = true, ClipText = true };
            var id = option.Id;
            var purpose = _state.Purpose;
            button.OnPressed += _ => SendMessage(new ImperialManufacturingSelectMessage(purpose, id));
            _list.AddChild(button);
        }
    }
}
