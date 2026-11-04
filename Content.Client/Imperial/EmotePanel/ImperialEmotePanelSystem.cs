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

/// <summary>Открывает панель по действию, запрашивает у сервера доступные эмоции и хранит свои эмоции игрока.</summary>
public sealed class ImperialEmotePanelSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ResPath SavePath = new("/imperial_emote_panel.txt");

    public readonly List<ImperialPanelEmote> Custom = new();

    /// <summary>Эмоции, доступные текущему существу (ui_static_data).</summary>
    public readonly List<ImperialEmoteEntry> Available = new();

    private ImperialEmotePanelWindow? _window;
    private bool _loaded;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialEmotePanelActionEvent>(OnAction);
        SubscribeNetworkEvent<ImperialEmoteListMessage>(OnList);
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
        Available.Clear();
        _window = new ImperialEmotePanelWindow(this);
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
        RaiseNetworkEvent(new ImperialEmoteListRequest());
    }

    private void OnList(ImperialEmoteListMessage msg)
    {
        Available.Clear();
        Available.AddRange(msg.Emotes);
        _window?.Refresh();
    }

    public string NameOf(string id) => Loc.GetString($"imperial-emote-name-{id}");

    public void Play(ImperialEmoteEntry emote, string? param) => RaiseNetworkEvent(new ImperialEmotePlayMessage(emote.Id, param));

    public void Preview(ImperialEmoteEntry emote) => RaiseNetworkEvent(new ImperialEmotePreviewMessage(emote.Id));

    public void Play(ImperialPanelEmote emote)
    {
        if (string.IsNullOrWhiteSpace(emote.Text))
        {
            if (emote.Emote is { } id)
                RaiseNetworkEvent(new ImperialEmotePlayMessage(id, null));
            return;
        }

        RaiseNetworkEvent(new ImperialCustomEmoteMessage(emote.Emote, emote.Text));
    }

    #region Хранение

    /// <summary>Строка: эмоция\tназвание\tтекст; табуляции и переводы строк из полей убираются.</summary>
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

            ProtoId<ImperialEmotePrototype>? emote = parts[0].Length > 0 && _proto.HasIndex<ImperialEmotePrototype>(parts[0]) ? parts[0] : null;
            var text = parts[2].Length > 0 ? parts[2] : null;
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
