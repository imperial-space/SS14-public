using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Blueprints;

/// <summary>
/// Окно чертежей: сохранить текущих призраков строительства под именем, поставить или удалить сохранённый чертёж,
/// открыть папку с файлами (ими можно делиться — это обычный YAML).
/// </summary>
public sealed class ImperialBlueprintWindow : DefaultWindow
{
    private readonly ImperialBlueprintSystem _system;
    private readonly Label _ghosts;
    private readonly LineEdit _name;
    private readonly Label _status;
    private readonly BoxContainer _list;

    public ImperialBlueprintWindow(ImperialBlueprintSystem system)
    {
        _system = system;
        Title = Loc.GetString("imperial-blueprint-window-title");
        MinSize = new Vector2(440, 420);
        SetSize = new Vector2(480, 520);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, Margin = new Thickness(4) };
        Contents.AddChild(root);

        root.AddChild(new Label { Text = Loc.GetString("imperial-blueprint-save-title"), StyleClasses = { StyleClass.LabelHeading } });
        _ghosts = new Label { StyleClasses = { StyleClass.LabelSubText } };
        root.AddChild(_ghosts);

        var saveRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        root.AddChild(saveRow);
        _name = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("imperial-blueprint-name-placeholder") };
        _name.OnTextEntered += _ => Save();
        saveRow.AddChild(_name);
        var save = new Button { Text = Loc.GetString("imperial-blueprint-save"), StyleClasses = { StyleClass.Positive } };
        save.OnPressed += _ => Save();
        saveRow.AddChild(save);

        _status = new Label { StyleClasses = { StyleClass.LabelSubText }, ClipText = true };
        root.AddChild(_status);

        root.AddChild(new PanelContainer { MinHeight = 2, StyleClasses = { StyleClass.LowDivider } });

        var listHeader = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        root.AddChild(listHeader);
        listHeader.AddChild(new Label { Text = Loc.GetString("imperial-blueprint-list-title"), StyleClasses = { StyleClass.LabelHeading }, HorizontalExpand = true });
        var refresh = new Button { Text = Loc.GetString("imperial-blueprint-refresh") };
        refresh.OnPressed += _ => Refresh();
        listHeader.AddChild(refresh);
        var folder = new Button { Text = Loc.GetString("imperial-blueprint-folder") };
        folder.OnPressed += _ => _system.OpenFolder();
        listHeader.AddChild(folder);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        scroll.AddChild(_list);
        root.AddChild(scroll);

        root.AddChild(new Label { Text = Loc.GetString("imperial-blueprint-hint"), StyleClasses = { StyleClass.LabelSubText } });
    }

    private void Save()
    {
        _system.TrySave(_name.Text, out var message);
        _status.Text = message;
        Refresh();
    }

    public void Refresh()
    {
        _ghosts.Text = Loc.GetString("imperial-blueprint-ghosts", ("count", _system.GhostCount()));

        _list.RemoveAllChildren();
        var blueprints = _system.List();
        if (blueprints.Count == 0)
        {
            _list.AddChild(new Label { Text = Loc.GetString("imperial-blueprint-list-empty"), StyleClasses = { StyleClass.LabelSubText } });
            return;
        }

        foreach (var blueprint in blueprints)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            var info = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
            info.AddChild(new Label { Text = blueprint.Name, ClipText = true });
            var size = blueprint.Size;
            info.AddChild(new Label
            {
                Text = Loc.GetString("imperial-blueprint-details",
                    ("count", blueprint.Entries.Count), ("width", size.X), ("height", size.Y),
                    ("author", blueprint.Author.Length > 0 ? blueprint.Author : "—")),
                StyleClasses = { StyleClass.LabelSubText },
                ClipText = true,
            });
            row.AddChild(info);

            var place = new Button { Text = Loc.GetString("imperial-blueprint-place"), StyleClasses = { StyleClass.Positive } };
            place.OnPressed += _ =>
            {
                _system.StartPlacing(blueprint);
                _status.Text = Loc.GetString("imperial-blueprint-placing", ("name", blueprint.Name));
            };
            row.AddChild(place);

            // Удаление — двойным нажатием, чтобы не стереть чертёж случайно.
            var delete = new Button { Text = Loc.GetString("imperial-blueprint-delete"), StyleClasses = { StyleClass.Negative } };
            var armed = false;
            delete.OnPressed += _ =>
            {
                if (!armed)
                {
                    armed = true;
                    delete.Text = Loc.GetString("imperial-blueprint-delete-confirm");
                    return;
                }

                _system.Delete(blueprint);
                _status.Text = Loc.GetString("imperial-blueprint-deleted", ("name", blueprint.Name));
                Refresh();
            };
            row.AddChild(delete);

            _list.AddChild(row);
        }
    }
}
