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
