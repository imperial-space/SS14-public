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
            top.AddChild(Tgui.Text(Loc.GetString("imperial-ai-vox-wait", ("seconds", (int)Math.Ceiling(cooldown))), Tgui.Average));

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
