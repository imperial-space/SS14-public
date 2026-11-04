using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.EmotePanel;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.EmotePanel;

/// <summary>EmotePanel TG + панель эмоций BandaStation: только эмоции, которые существо может выполнить.</summary>
public sealed class ImperialEmotePanelWindow : DefaultWindow
{
    private readonly ImperialEmotePanelSystem _system;

    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private readonly LineEdit _param = new() { HorizontalExpand = true };
    private readonly BoxContainer _custom = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    private readonly BoxContainer _groups = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly BoxContainer _editor = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, Visible = false, SeparationOverride = 4 };
    private readonly LineEdit _editName = new() { PlaceHolder = Loc.GetString("imperial-emote-panel-name") };
    private readonly LineEdit _editText = new() { PlaceHolder = Loc.GetString("imperial-emote-panel-text") };
    private readonly OptionButton _editBase = new();
    private readonly List<string?> _baseOptions = new();

    private readonly Button _filterVisible = Filter("imperial-emote-panel-filter-visible");
    private readonly Button _filterAudible = Filter("imperial-emote-panel-filter-audible");
    private readonly Button _filterSound = Filter("imperial-emote-panel-filter-sound");
    private readonly Button _filterHands = Filter("imperial-emote-panel-filter-hands");
    private readonly Button _filterParams = Filter("imperial-emote-panel-filter-params");

    private ImperialPanelEmote? _editing;

    public ImperialEmotePanelWindow(ImperialEmotePanelSystem system)
    {
        _system = system;

        Title = Loc.GetString("imperial-emote-panel-title");
        MinSize = new Vector2(460, 380);
        SetSize = new Vector2(620, 660);

        _search.PlaceHolder = Loc.GetString("imperial-emote-panel-search");
        _search.OnTextChanged += _ => RebuildGroups();
        _param.PlaceHolder = Loc.GetString("imperial-emote-panel-param");

        var filters = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2 };
        foreach (var button in new[] { _filterVisible, _filterAudible, _filterSound, _filterHands, _filterParams })
        {
            button.OnToggled += _ => RebuildGroups();
            filters.AddChild(button);
        }

        BuildEditor();

        var addButton = new Button { Text = Loc.GetString("imperial-emote-panel-add") };
        addButton.OnPressed += _ => OpenEditor(null);

        var customHeader = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        customHeader.AddChild(new Label { Text = Loc.GetString("imperial-emote-panel-custom"), HorizontalExpand = true, StyleClasses = { "LabelHeading" } });
        customHeader.AddChild(addButton);

        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        content.AddChild(customHeader);
        content.AddChild(_editor);
        content.AddChild(_custom);
        content.AddChild(_groups);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(content);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        root.AddChild(_search);
        root.AddChild(filters);
        root.AddChild(_param);
        root.AddChild(scroll);
        Contents.AddChild(root);

        Refresh();
    }

    private static Button Filter(string loc)
    {
        return new Button { Text = Loc.GetString(loc), ToggleMode = true, HorizontalExpand = true, ClipText = true };
    }

    public void Refresh()
    {
        RebuildCustom();
        RebuildGroups();
    }

    #region Эмоции

    private IEnumerable<ImperialEmoteEntry> Filtered()
    {
        var search = _search.Text.Trim();
        return _system.Available
            .Where(e => (!_filterVisible.Pressed || e.Visible) &&
                        (!_filterAudible.Pressed || e.Audible) &&
                        (!_filterSound.Pressed || e.Sound) &&
                        (!_filterHands.Pressed || e.Hands) &&
                        (!_filterParams.Pressed || e.Param))
            .Where(e => search.Length == 0 ||
                        _system.NameOf(e.Id).Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                        e.Id.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => _system.NameOf(e.Id));
    }

    /// <summary>Разделы как в панели BandaStation: ○ действия, ▷ со звуком, ◦ без звука.</summary>
    private void RebuildGroups()
    {
        _groups.RemoveAllChildren();
        if (_system.Available.Count == 0)
        {
            _groups.AddChild(new Label { Text = Loc.GetString("imperial-emote-panel-loading"), FontColorOverride = Color.Gray });
            return;
        }

        var emotes = Filtered().ToList();
        AddGroup("imperial-emote-panel-group-action", emotes.Where(e => e.Action));
        AddGroup("imperial-emote-panel-group-sound", emotes.Where(e => !e.Action && e.Sound));
        AddGroup("imperial-emote-panel-group-silent", emotes.Where(e => !e.Action && !e.Sound));
    }

    private void AddGroup(string title, IEnumerable<ImperialEmoteEntry> emotes)
    {
        var grid = new GridContainer { Columns = 3, HorizontalExpand = true };
        foreach (var emote in emotes)
            grid.AddChild(EmoteRow(emote));

        if (grid.ChildCount == 0)
            return;

        _groups.AddChild(new Label { Text = Loc.GetString(title), StyleClasses = { "LabelHeading" } });
        _groups.AddChild(grid);
    }

    private Control EmoteRow(ImperialEmoteEntry emote)
    {
        var name = _system.NameOf(emote.Id);
        var play = new Button { Text = name, HorizontalExpand = true, ClipText = true, ToolTip = Flags(emote) };
        play.OnPressed += _ =>
        {
            var param = _param.Text.Trim();
            _system.Play(emote, emote.Param && param.Length > 0 ? param : null);
        };

        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
        row.AddChild(play);

        if (emote.Sound)
        {
            var preview = new Button { Text = "♪", ToolTip = Loc.GetString("imperial-emote-panel-preview") };
            preview.OnPressed += _ => _system.Preview(emote);
            row.AddChild(preview);
        }

        var favourite = new Button { Text = "★", ToolTip = Loc.GetString("imperial-emote-panel-favourite") };
        favourite.OnPressed += _ => AddCustom(new ImperialPanelEmote { Name = name, Emote = emote.Id });
        row.AddChild(favourite);
        return row;
    }

    /// <summary>Значки EmotePanel TG: видимая, слышимая, звук, руки, параметр.</summary>
    private static string Flags(ImperialEmoteEntry emote)
    {
        var flags = new List<string>();
        if (emote.Visible)
            flags.Add(Loc.GetString("imperial-emote-panel-filter-visible"));
        if (emote.Audible)
            flags.Add(Loc.GetString("imperial-emote-panel-filter-audible"));
        if (emote.Sound)
            flags.Add(Loc.GetString("imperial-emote-panel-filter-sound"));
        if (emote.Hands)
            flags.Add(Loc.GetString("imperial-emote-panel-filter-hands"));
        if (emote.Param)
            flags.Add(Loc.GetString("imperial-emote-panel-filter-params"));
        return string.Join(", ", flags);
    }

    #endregion

    #region Свои эмоции

    private void RebuildCustom()
    {
        _custom.RemoveAllChildren();
        if (_system.Custom.Count == 0)
        {
            _custom.AddChild(new Label { Text = Loc.GetString("imperial-emote-panel-custom-empty"), FontColorOverride = Color.Gray });
            return;
        }

        foreach (var emote in _system.Custom)
        {
            var play = new Button { Text = emote.Name, HorizontalExpand = true, ClipText = true, ToolTip = Description(emote) };
            play.OnPressed += _ => _system.Play(emote);

            var edit = new Button { Text = "✎", ToolTip = Loc.GetString("imperial-emote-panel-edit") };
            edit.OnPressed += _ => OpenEditor(emote);

            var up = new Button { Text = "▲", ToolTip = Loc.GetString("imperial-emote-panel-up") };
            up.OnPressed += _ => Move(emote, -1);

            var delete = new Button { Text = "✕", ToolTip = Loc.GetString("imperial-emote-panel-delete") };
            delete.OnPressed += _ =>
            {
                _system.Custom.Remove(emote);
                _system.Save();
                RebuildCustom();
            };

            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            row.AddChild(play);
            row.AddChild(up);
            row.AddChild(edit);
            row.AddChild(delete);
            _custom.AddChild(row);
        }
    }

    private string Description(ImperialPanelEmote emote)
    {
        var baseName = emote.Emote is { } id ? _system.NameOf(id) : null;
        return (baseName, emote.Text) switch
        {
            (null, { } text) => Loc.GetString("imperial-emote-panel-desc-me", ("text", text)),
            ({ } name, { } text) => Loc.GetString("imperial-emote-panel-desc-custom", ("emote", name), ("text", text)),
            ({ } name, null) => Loc.GetString("imperial-emote-panel-desc-default", ("emote", name)),
            _ => string.Empty,
        };
    }

    private void Move(ImperialPanelEmote emote, int offset)
    {
        var index = _system.Custom.IndexOf(emote);
        var target = Math.Clamp(index + offset, 0, _system.Custom.Count - 1);
        if (index < 0 || target == index)
            return;

        _system.Custom.RemoveAt(index);
        _system.Custom.Insert(target, emote);
        _system.Save();
        RebuildCustom();
    }

    private void AddCustom(ImperialPanelEmote emote)
    {
        if (_system.Custom.Count >= ImperialEmotePanel.MaxEmotes)
            return;

        _system.Custom.Add(emote);
        _system.Save();
        RebuildCustom();
    }

    private void BuildEditor()
    {
        _editName.OnTextChanged += _ =>
        {
            if (_editName.Text.Length > ImperialEmotePanel.MaxNameLength)
                _editName.Text = _editName.Text[..ImperialEmotePanel.MaxNameLength];
        };
        _editText.OnTextChanged += _ =>
        {
            if (_editText.Text.Length > ImperialEmotePanel.MaxTextLength)
                _editText.Text = _editText.Text[..ImperialEmotePanel.MaxTextLength];
        };
        _editBase.OnItemSelected += args => _editBase.SelectId(args.Id);

        var save = new Button { Text = Loc.GetString("imperial-emote-panel-save"), HorizontalExpand = true };
        save.OnPressed += _ => SaveEditor();
        var cancel = new Button { Text = Loc.GetString("imperial-emote-panel-cancel"), HorizontalExpand = true };
        cancel.OnPressed += _ => _editor.Visible = false;

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        buttons.AddChild(save);
        buttons.AddChild(cancel);

        _editor.AddChild(_editName);
        _editor.AddChild(_editText);
        _editor.AddChild(new Label { Text = Loc.GetString("imperial-emote-panel-base"), FontColorOverride = Color.Gray });
        _editor.AddChild(_editBase);
        _editor.AddChild(buttons);
    }

    private void OpenEditor(ImperialPanelEmote? emote)
    {
        _editing = emote;
        _editName.Text = emote?.Name ?? string.Empty;
        _editText.Text = emote?.Text ?? string.Empty;

        _editBase.Clear();
        _baseOptions.Clear();
        _editBase.AddItem(Loc.GetString("imperial-emote-panel-base-none"), 0);
        _baseOptions.Add(null);
        var selected = 0;
        foreach (var entry in _system.Available.OrderBy(e => _system.NameOf(e.Id)))
        {
            _baseOptions.Add(entry.Id);
            _editBase.AddItem(_system.NameOf(entry.Id), _baseOptions.Count - 1);
            if (emote?.Emote == entry.Id)
                selected = _baseOptions.Count - 1;
        }

        _editBase.SelectId(selected);
        _editor.Visible = true;
    }

    private void SaveEditor()
    {
        var name = _editName.Text.Trim();
        var text = _editText.Text.Trim();
        var baseEmote = _baseOptions[_editBase.SelectedId];
        if (name.Length == 0)
            name = text.Length > 0 ? text : baseEmote != null ? _system.NameOf(baseEmote) : string.Empty;
        if (name.Length == 0 || text.Length == 0 && baseEmote == null)
            return;

        if (name.Length > ImperialEmotePanel.MaxNameLength)
            name = name[..ImperialEmotePanel.MaxNameLength];

        var emote = _editing ?? new ImperialPanelEmote();
        emote.Name = name;
        emote.Text = text.Length > 0 ? text : null;
        emote.Emote = baseEmote;
        _editor.Visible = false;

        if (_editing == null)
        {
            AddCustom(emote);
            return;
        }

        _system.Save();
        RebuildCustom();
    }

    #endregion
}
