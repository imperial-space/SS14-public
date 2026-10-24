using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.StationAi;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.StationAi;

/// <summary>
/// VOX: слова объявления звучат по очереди (CHANNEL_VOX с wait), окно объявления со словарём и предпрослушиванием,
/// окно картинок для экранов и слой картинки поверх экрана.
/// </summary>
public sealed class ImperialStationAiVoxSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly ResPath DisplayRsi = new("/Textures/Imperial/StationAi/status_display.rsi");
    private const string DisplayLayer = "imperial-ai-display";

    private readonly Queue<string> _queue = new();
    private TimeSpan _nextWord;
    private List<string>? _words;
    private DefaultWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialAiVoxPlayEvent>(OnVox);
        SubscribeNetworkEvent<ImperialAiAnnouncementOpenEvent>(OnAnnouncementOpen);
        SubscribeNetworkEvent<ImperialAiStatusDisplayOpenEvent>(OnDisplayOpen);

        SubscribeLocalEvent<ImperialAiDisplayComponent, ComponentStartup>((uid, comp, _) => UpdateDisplay((uid, comp)));
        SubscribeLocalEvent<ImperialAiDisplayComponent, AfterAutoHandleStateEvent>((uid, comp, _) => UpdateDisplay((uid, comp)));
        SubscribeLocalEvent<ImperialAiDisplayComponent, ComponentShutdown>(OnDisplayShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }

    #region Голос

    private void OnVox(ImperialAiVoxPlayEvent ev)
    {
        foreach (var word in ev.Words)
        {
            _queue.Enqueue(ImperialAiVox.PathOf(word));
        }
    }

    public void Preview(string word)
    {
        Play(ImperialAiVox.PathOf(word));
    }

    private TimeSpan Play(string path)
    {
        _audio.PlayGlobal(new SoundPathSpecifier(path), Filter.Local(), false);
        return _audio.GetAudioLength(path);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_queue.Count == 0 || _timing.RealTime < _nextWord)
            return;

        _nextWord = _timing.RealTime + Play(_queue.Dequeue());
    }

    /// <summary>GLOB.vox_sounds: слова по файлам словаря.</summary>
    public List<string> Words()
    {
        return _words ??= _resources.ContentFindFiles(new ResPath(ImperialAiVox.Directory))
            .Where(p => p.Extension == "ogg")
            .Select(p => p.FilenameWithoutExtension switch
            {
                "_comma" => ",",
                "_period" => ".",
                var name => name,
            })
            .Order()
            .ToList();
    }

    #endregion

    #region Окна

    private void Show(DefaultWindow window)
    {
        _window?.Close();
        _window = window;
        window.OnClose += () =>
        {
            if (_window == window)
                _window = null;
        };
        window.OpenCentered();
    }

    private void OnAnnouncementOpen(ImperialAiAnnouncementOpenEvent ev)
    {
        var window = new ImperialAiAnnouncementWindow(this, ev.LastMessage, ev.Cooldown);
        window.OnAnnounce += message =>
        {
            RaiseNetworkEvent(new ImperialAiAnnouncementEvent(message));
            window.Close();
        };
        Show(window);
    }

    private void OnDisplayOpen(ImperialAiStatusDisplayOpenEvent ev)
    {
        var window = new ImperialAiStatusDisplayWindow(_sprite, ev.Current);
        window.OnChosen += state => RaiseNetworkEvent(new ImperialAiStatusDisplayChoiceEvent(state));
        Show(window);
    }

    #endregion

    #region Экраны

    private void UpdateDisplay(Entity<ImperialAiDisplayComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!_sprite.LayerMapTryGet((ent, sprite), DisplayLayer, out var layer, false))
        {
            layer = _sprite.AddLayer((ent, sprite), new SpriteSpecifier.Rsi(DisplayRsi, ent.Comp.State ?? "ai_off"));
            _sprite.LayerMapSet((ent, sprite), DisplayLayer, layer);
        }

        if (ent.Comp.State != null)
            _sprite.LayerSetRsiState((ent, sprite), layer, ent.Comp.State);
        _sprite.LayerSetVisible((ent, sprite), layer, ent.Comp.State != null);
    }

    private void OnDisplayShutdown(Entity<ImperialAiDisplayComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite) && _sprite.LayerMapTryGet((ent, sprite), DisplayLayer, out var layer, false))
            _sprite.RemoveLayer((ent, sprite), layer);
    }

    #endregion
}

/// <summary>announcement() + announcement_help(): текст до 30 слов и словарь VOX с прослушиванием.</summary>
public sealed class ImperialAiAnnouncementWindow : DefaultWindow
{
    public event Action<string>? OnAnnounce;

    private readonly ImperialStationAiVoxSystem _vox;
    private readonly LineEdit _message = new() { HorizontalExpand = true };
    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private readonly GridContainer _words = new() { Columns = 4, HSeparationOverride = 2, VSeparationOverride = 2 };

    public ImperialAiAnnouncementWindow(ImperialStationAiVoxSystem vox, string last, float cooldown)
    {
        _vox = vox;
        Title = Loc.GetString("imperial-ai-vox-title");
        MinSize = SetSize = new Vector2(560, 520);

        _message.Text = last;
        _message.PlaceHolder = Loc.GetString("imperial-ai-vox-placeholder");
        _message.OnTextEntered += _ => Announce();

        var send = Tgui.Button(Loc.GetString("imperial-ai-vox-send"), Announce, Tgui.Good, disabled: cooldown > 0);
        var top = Tgui.VBox(6,
            Tgui.Paragraph(Loc.GetString("imperial-ai-vox-warning"), Tgui.Bad),
            Tgui.HBox(6, _message, send));
        if (cooldown > 0)
            top.AddChild(Tgui.Text(Loc.GetString("imperial-ai-vox-wait", ("seconds", (int) Math.Ceiling(cooldown))), Tgui.Average));

        _search.PlaceHolder = Loc.GetString("imperial-ai-list-search");
        _search.OnTextChanged += _ => RebuildWords();
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_words);
        var help = Tgui.VBox(4, Tgui.Paragraph(Loc.GetString("imperial-ai-vox-help"), Tgui.Label), _search, scroll);
        help.VerticalExpand = true;
        var helpSection = Tgui.MakeSection(Loc.GetString("imperial-ai-vox-help-title"), null, help);
        helpSection.VerticalExpand = true;

        var root = Tgui.VBox(6, Tgui.MakeSection(Loc.GetString("imperial-ai-vox-title"), null, top), helpSection);
        root.Margin = new Thickness(6);
        root.VerticalExpand = true;
        Contents.AddChild(Tgui.Window(root, scrollable: false));
        RebuildWords();
    }

    private void Announce()
    {
        if (_message.Text.Trim().Length > 0)
            OnAnnounce?.Invoke(_message.Text);
    }

    private void RebuildWords()
    {
        _words.RemoveAllChildren();
        var query = _search.Text.Trim().ToLowerInvariant();
        foreach (var word in _vox.Words().Where(w => query.Length == 0 || w.Contains(query)))
        {
            var current = word;
            var button = new TguiButton(word, Tgui.Transparent) { HorizontalExpand = true };
            button.TextLabel.HorizontalAlignment = Control.HAlignment.Left;
            button.ToolTip = Loc.GetString("imperial-ai-vox-preview");
            button.OnPressed += _ => _vox.Preview(current);
            _words.AddChild(button);
        }
    }
}

/// <summary>AiStatusDisplayPicker: картинки статус-дисплеев с подписями, текущая подсвечена.</summary>
public sealed class ImperialAiStatusDisplayWindow : DefaultWindow
{
    public event Action<string?>? OnChosen;

    private static readonly ResPath Rsi = new("/Textures/Imperial/StationAi/status_display.rsi");

    public ImperialAiStatusDisplayWindow(SpriteSystem sprites, string? current)
    {
        Title = Loc.GetString("imperial-ai-display-title");
        MinSize = SetSize = new Vector2(480, 520);

        var grid = new GridContainer { Columns = 4, HSeparationOverride = 6, VSeparationOverride = 6 };
        var none = Tgui.Button(Loc.GetString("imperial-ai-display-none"), () => Choose(null), current == null ? Tgui.Green : null);
        none.MinSize = new Vector2(100, 92);
        grid.AddChild(none);

        foreach (var (name, state) in ImperialAiVox.DisplayOptions.DistinctBy(o => o.Name))
        {
            var option = state;
            var button = new TguiButton(string.Empty, option == current ? Tgui.Green : Tgui.Transparent) { MinSize = new Vector2(100, 92) };
            button.TextLabel.Visible = false;
            var icon = new TextureRect
            {
                Texture = sprites.Frame0(new SpriteSpecifier.Rsi(Rsi, state)),
                TextureScale = new Vector2(2, 2),
                Stretch = TextureRect.StretchMode.KeepCentered,
                MinSize = new Vector2(64, 64),
            };
            var label = Tgui.Text(name, size: 10, align: Control.HAlignment.Center);
            var box = Tgui.VBox(2, icon, label);
            box.Margin = new Thickness(2);
            button.AddChild(box);
            button.OnPressed += _ => Choose(option);
            grid.AddChild(button);
        }

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(grid);
        var section = Tgui.MakeSection(Loc.GetString("imperial-ai-display-title"), null, scroll);
        section.VerticalExpand = true;
        var root = Tgui.VBox(0, section);
        root.Margin = new Thickness(6);
        root.VerticalExpand = true;
        Contents.AddChild(Tgui.Window(root, scrollable: false));
    }

    private void Choose(string? state)
    {
        OnChosen?.Invoke(state);
        Close();
    }
}
