using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.StationAi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.StationAi;

/// <summary>checklaws(): галочки у законов, выбор канала и «Огласить законы».</summary>
public sealed class ImperialAiLawsWindow : DefaultWindow
{
    public event Action<List<int>, string?>? OnState;

    private readonly HashSet<int> _checked;
    private string? _channel;
    private readonly BoxContainer _root = Tgui.VBox(6);
    private readonly List<string> _laws;
    private readonly List<ImperialAiChannelEntry> _channels;

    public ImperialAiLawsWindow(List<string> laws, List<ImperialAiChannelEntry> channels)
    {
        Title = Loc.GetString("imperial-ai-laws-title");
        MinSize = SetSize = new Vector2(450, 420);
        _laws = laws;
        _channels = channels;
        _checked = Enumerable.Range(0, laws.Count).ToHashSet();
        _root.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(_root));
        Rebuild();
    }

    private void Rebuild()
    {
        _root.RemoveAllChildren();

        var lawList = Tgui.VBox(4);
        for (var i = 0; i < _laws.Count; i++)
        {
            var index = i;
            var check = TguiLayout.Checkbox(string.Empty, _checked.Contains(index), () =>
            {
                if (!_checked.Remove(index))
                    _checked.Add(index);
                Rebuild();
            });
            check.VerticalAlignment = VAlignment.Top;

            var text = Tgui.Paragraph(_laws[i]);
            text.HorizontalExpand = true;
            lawList.AddChild(Tgui.HBox(6, check, text));
        }

        _root.AddChild(Tgui.MakeSection(Loc.GetString("imperial-ai-laws-title"), null, lawList));

        var channels = new GridContainer { Columns = 3, HSeparationOverride = 4, VSeparationOverride = 4 };
        channels.AddChild(Tgui.Button(Loc.GetString("imperial-ai-laws-local"), () =>
        {
            _channel = null;
            Rebuild();
        }, _channel == null ? Tgui.Green : null));

        foreach (var channel in _channels)
        {
            var id = channel.Id;
            channels.AddChild(Tgui.Button(channel.Name, () =>
            {
                _channel = id;
                Rebuild();
            }, _channel == id ? Tgui.Green : null));
        }

        _root.AddChild(Tgui.MakeSection(Loc.GetString("imperial-ai-laws-channel"), null, channels));

        var state = Tgui.Button(Loc.GetString("imperial-ai-laws-state"),
            () => OnState?.Invoke(_checked.Order().ToList(), _channel), Tgui.Good, disabled: _checked.Count == 0, fluid: true);
        state.MinHeight = 28;
        _root.AddChild(state);
    }
}
