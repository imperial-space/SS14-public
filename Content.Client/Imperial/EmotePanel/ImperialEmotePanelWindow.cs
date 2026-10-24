using System.IO;
using System.Linq;
using System.Numerics;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Imperial.EmotePanel;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.EmotePanel;

/// <summary>Своя эмоция на панели: стандартная, стандартная со своим текстом или просто текст.</summary>
public sealed class ImperialPanelEmote
{
    public string Name = string.Empty;
    public ProtoId<EmotePrototype>? Emote;
    public string? Text;
}

/// <summary>Открывает панель по действию и хранит свои эмоции игрока у него на компьютере.</summary>
public sealed class ImperialEmotePanelSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ResPath SavePath = new("/imperial_emote_panel.txt");

    public readonly List<ImperialPanelEmote> Custom = new();
    private ImperialEmotePanelWindow? _window;
    private bool _loaded;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialEmotePanelActionEvent>(OnAction);
    }

    private void OnAction(ImperialEmotePanelActionEvent args)
    {
        args.Handled = true;
        if (_window != null)
        {
            _window.Close();
            return;
        }

        Load();
        _window = new ImperialEmotePanelWindow(this);
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }

    public EntityUid? Local => _player.LocalEntity;

    public void Play(EmotePrototype emote) => RaisePredictiveEvent(new PlayEmoteMessage(emote.ID));

    public void Play(ImperialPanelEmote emote)
    {
        if (string.IsNullOrWhiteSpace(emote.Text))
        {
            if (emote.Emote is { } id && _proto.TryIndex(id, out var proto))
                Play(proto);
            return;
        }

        RaiseNetworkEvent(new ImperialCustomEmoteMessage(emote.Emote, emote.Text));
    }

    #region Хранение

    /// <summary>Строка: тип\tэмоция\tназвание\tтекст; табуляции и переводы строк из полей убираются.</summary>
    private void Load()
    {
        if (_loaded)
            return;

        _loaded = true;
        Custom.Clear();
        if (!_resource.UserData.Exists(SavePath))
            return;

        using var reader = _resource.UserData.OpenText(SavePath);
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split('\t');
            if (parts.Length < 3 || Custom.Count >= ImperialEmotePanel.MaxEmotes)
                continue;

            ProtoId<EmotePrototype>? emote = parts[0].Length > 0 && _proto.HasIndex<EmotePrototype>(parts[0]) ? parts[0] : null;
            var text = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null;
            if (emote == null && text == null)
                continue;

            Custom.Add(new ImperialPanelEmote { Emote = emote, Name = parts[1], Text = text });
        }
    }

    public void Save()
    {
        using var writer = _resource.UserData.OpenWriteText(SavePath);
        foreach (var emote in Custom)
        {
            writer.WriteLine($"{emote.Emote?.Id ?? string.Empty}\t{Clean(emote.Name)}\t{Clean(emote.Text ?? string.Empty)}");
        }
    }

    private static string Clean(string text) => text.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

    #endregion
}

/// <summary>EmotePanel TG + панель своих эмоций BandaStation: поиск, категории, избранное и свои тексты.</summary>
public sealed class ImperialEmotePanelWindow : DefaultWindow
{
    private readonly ImperialEmotePanelSystem _system;
    private readonly IPrototypeManager _proto;
    private readonly SharedChatSystem _chat;
    private readonly SpriteSystem _sprite;

    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private readonly BoxContainer _custom = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    private readonly GridContainer _all = new() { Columns = 3, HorizontalExpand = true };
    private readonly BoxContainer _editor = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, Visible = false, SeparationOverride = 4 };
    private readonly LineEdit _editName = new() { PlaceHolder = Loc.GetString("imperial-emote-panel-name") };
    private readonly LineEdit _editText = new() { PlaceHolder = Loc.GetString("imperial-emote-panel-text") };
    private readonly OptionButton _editBase = new();
    private readonly List<EmotePrototype?> _baseOptions = new();
    private EmoteCategory? _category;
    private ImperialPanelEmote? _editing;

    public ImperialEmotePanelWindow(ImperialEmotePanelSystem system)
    {
        _system = system;
        _proto = IoCManager.Resolve<IPrototypeManager>();
        var entities = IoCManager.Resolve<IEntityManager>();
        _chat = entities.System<SharedChatSystem>();
        _sprite = entities.System<SpriteSystem>();

        Title = Loc.GetString("imperial-emote-panel-title");
        MinSize = new Vector2(420, 360);
        SetSize = new Vector2(560, 620);

        _search.PlaceHolder = Loc.GetString("imperial-emote-panel-search");
        _search.OnTextChanged += _ => RebuildAll();

        var filters = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2 };
        AddFilter(filters, "imperial-emote-panel-filter-all", null);
        AddFilter(filters, "emote-menu-category-general", EmoteCategory.General);
        AddFilter(filters, "emote-menu-category-hands", EmoteCategory.Hands);
        AddFilter(filters, "emote-menu-category-vocal", EmoteCategory.Vocal);

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
        content.AddChild(new Label { Text = Loc.GetString("imperial-emote-panel-all"), StyleClasses = { "LabelHeading" } });
        content.AddChild(_all);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(content);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        root.AddChild(_search);
        root.AddChild(filters);
        root.AddChild(scroll);
        Contents.AddChild(root);

        RebuildCustom();
        RebuildAll();
    }

    private void AddFilter(BoxContainer box, string text, EmoteCategory? category)
    {
        var button = new Button { Text = Loc.GetString(text), ToggleMode = true, Pressed = category == _category, HorizontalExpand = true };
        button.OnPressed += _ =>
        {
            _category = category;
            foreach (var child in box.Children.OfType<Button>())
                child.Pressed = child == button;
            RebuildAll();
        };
        box.AddChild(button);
    }

    private IEnumerable<EmotePrototype> Available()
    {
        if (_system.Local is not { } local)
            return Enumerable.Empty<EmotePrototype>();

        return _proto.EnumeratePrototypes<EmotePrototype>()
            .Where(e => e.Category != EmoteCategory.Invalid && _chat.AllowedToUseEmote(local, e))
            .OrderBy(e => Loc.GetString(e.Name));
    }

    #region Все эмоции

    private void RebuildAll()
    {
        _all.RemoveAllChildren();
        var search = _search.Text.Trim();
        foreach (var emote in Available())
        {
            var name = Loc.GetString(emote.Name);
            if (_category != null && emote.Category != _category)
                continue;
            if (search.Length > 0 && !name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                && !emote.ChatTriggers.Any(t => t.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
                continue;

            var play = new Button { HorizontalExpand = true, ToolTip = Loc.GetString("imperial-emote-panel-play-tooltip") };
            var inner = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            inner.AddChild(new TextureRect { Texture = _sprite.Frame0(emote.Icon), TextureScale = new Vector2(1, 1), Stretch = TextureRect.StretchMode.KeepCentered, MinSize = new Vector2(24, 24) });
            inner.AddChild(new Label { Text = name, ClipText = true, HorizontalExpand = true });
            play.AddChild(inner);
            var proto = emote;
            play.OnPressed += _ => _system.Play(proto);

            var favourite = new Button { Text = "★", ToolTip = Loc.GetString("imperial-emote-panel-favourite") };
            favourite.OnPressed += _ => AddCustom(new ImperialPanelEmote { Name = name, Emote = proto.ID });

            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
            row.AddChild(play);
            row.AddChild(favourite);
            _all.AddChild(row);
        }
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
        var baseName = emote.Emote is { } id && _proto.TryIndex(id, out var proto) ? Loc.GetString(proto.Name) : null;
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
        foreach (var proto in Available())
        {
            _baseOptions.Add(proto);
            _editBase.AddItem(Loc.GetString(proto.Name), _baseOptions.Count - 1);
            if (emote?.Emote == proto.ID)
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
            name = text.Length > 0 ? text : baseEmote != null ? Loc.GetString(baseEmote.Name) : string.Empty;
        if (name.Length == 0 || text.Length == 0 && baseEmote == null)
            return;

        if (name.Length > ImperialEmotePanel.MaxNameLength)
            name = name[..ImperialEmotePanel.MaxNameLength];

        var emote = _editing ?? new ImperialPanelEmote();
        emote.Name = name;
        emote.Text = text.Length > 0 ? text : null;
        emote.Emote = baseEmote?.ID;
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
