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

/// <summary>Окно сортировщика (tgui ManufacturingSorter).</summary>
public sealed class ImperialManufacturingSorterBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private BoxContainer _filters = default!;
    private Button _unmet = default!;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-sorter-ui-title");
        _window.SetSize = new Vector2(450, 350);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        header.AddChild(new Label { Text = Loc.GetString("imperial-sorter-ui-filters"), HorizontalExpand = true, StyleClasses = { "LabelHeading" } });
        var add = new Button { Text = "+ " + Loc.GetString("imperial-sorter-ui-new-filter"), ModulateSelfOverride = Color.FromHex("#5baa27") };
        add.OnPressed += _ => SendMessage(new ImperialSorterActionMessage(ImperialSorterAction.NewFilter));
        header.AddChild(add);

        _filters = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_filters);

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        body.AddChild(header);
        body.AddChild(scroll);
        var section = ImperialFishingUi.Section(null, body);
        section.VerticalExpand = true;
        root.AddChild(section);

        var bottom = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        bottom.AddChild(new Label { Text = Loc.GetString("imperial-sorter-ui-unmet"), VerticalAlignment = Control.VAlignment.Center });
        _unmet = new Button { MinWidth = 32, Margin = new Thickness(4, 0, 0, 0) };
        _unmet.OnPressed += _ => SendMessage(new ImperialSorterActionMessage(ImperialSorterAction.RotateUnmet));
        bottom.AddChild(_unmet);
        root.AddChild(bottom);

        _window.Contents.AddChild(root);
    }

    /// <summary>dir2icon().</summary>
    private static string Arrow(Direction dir) => dir switch
    {
        Direction.South => "↓",
        Direction.East => "→",
        Direction.West => "←",
        _ => "↑",
    };

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialSorterState sorter || _window == null)
            return;

        _unmet.Text = Arrow(sorter.UnmetDir);
        _filters.RemoveAllChildren();

        for (var i = 0; i < sorter.Filters.Count; i++)
        {
            var filter = sorter.Filters[i];
            var id = filter.Id;
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 1) };
            row.AddChild(new Label { Text = $"{i + 1}. {filter.Name}", HorizontalExpand = true, ClipText = true });

            var output = new Button { Text = Arrow(filter.Dir) + " " + Loc.GetString("imperial-sorter-ui-output") };
            output.OnPressed += _ => SendMessage(new ImperialSorterActionMessage(ImperialSorterAction.Rotate, id));
            row.AddChild(output);

            var edit = new Button { Text = "✎", ToolTip = Loc.GetString("imperial-sorter-ui-edit") };
            var textValue = filter.TextValue;
            edit.OnPressed += _ =>
            {
                if (textValue != null)
                    OpenTextEdit(id, textValue);
                else
                    SendMessage(new ImperialSorterActionMessage(ImperialSorterAction.Edit, id));
            };
            row.AddChild(edit);

            var delete = new Button { Text = "✖", ModulateSelfOverride = Color.FromHex("#bd2020"), ToolTip = Loc.GetString("imperial-sorter-ui-delete") };
            delete.OnPressed += _ => SendMessage(new ImperialSorterActionMessage(ImperialSorterAction.DelFilter, id));
            row.AddChild(delete);

            var up = new Button { Text = "↑" };
            up.OnPressed += _ => SendMessage(new ImperialSorterActionMessage(ImperialSorterAction.ShiftUp, id));
            row.AddChild(up);

            var down = new Button { Text = "↓" };
            down.OnPressed += _ => SendMessage(new ImperialSorterActionMessage(ImperialSorterAction.ShiftDown, id));
            row.AddChild(down);

            _filters.AddChild(row);
        }
    }

    /// <summary>tgui_input_text() фильтра name_contains.</summary>
    private void OpenTextEdit(int id, string value)
    {
        var dialog = new DefaultWindow { Title = Loc.GetString("imperial-sorter-ui-name-title"), SetSize = new Vector2(300, 110) };
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(new Label { Text = Loc.GetString("imperial-sorter-ui-name-prompt") });
        var line = new LineEdit { Text = value, HorizontalExpand = true, IsValid = s => s.Length <= 12 };
        box.AddChild(line);
        var ok = new Button { Text = Loc.GetString("imperial-sorter-ui-confirm"), HorizontalAlignment = Control.HAlignment.Right };
        box.AddChild(ok);
        dialog.Contents.AddChild(box);

        void Confirm()
        {
            SendMessage(new ImperialSorterTextMessage(id, line.Text));
            dialog.Close();
        }

        ok.OnPressed += _ => Confirm();
        line.OnTextEntered += _ => Confirm();
        dialog.OpenCentered();
        line.GrabKeyboardFocus();
    }
}
