using System.Numerics;
using Content.Shared.Imperial.TerrorSpider;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.TerrorSpider;

/// <summary>tgui_input_list / input() пауков Ужаса: тип яиц, размер кладки, цель обзора или кокона.</summary>
public sealed class TerrorSpiderChoiceBoundUserInterface : BoundUserInterface
{
    private TerrorSpiderChoiceWindow? _window;

    public TerrorSpiderChoiceBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<TerrorSpiderChoiceWindow>();
        _window.Chosen += (mode, key, count, percent) => SendMessage(new TerrorSpiderChoiceMessage(mode, key, count, percent));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is TerrorSpiderChoiceBoundUserInterfaceState choice)
            _window?.UpdateState(choice);
    }
}

public sealed class TerrorSpiderChoiceWindow : DefaultWindow
{
    public event Action<TerrorSpiderChoiceMode, string, int, int>? Chosen;

    private readonly BoxContainer _body = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private TerrorSpiderChoiceBoundUserInterfaceState? _state;
    private int _count;
    private int _percent;

    public TerrorSpiderChoiceWindow()
    {
        MinSize = new Vector2(320, 200);
        SetSize = new Vector2(340, 420);
        Contents.AddChild(new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            Children = { _body },
        });
    }

    public void UpdateState(TerrorSpiderChoiceBoundUserInterfaceState state)
    {
        _state = state;
        Title = state.Title;
        _count = state.Counts.Length > 0 ? state.Counts[0] : 1;
        _percent = state.Percents.Length > 0 ? state.Percents[0] : 0;
        Rebuild();
    }

    private void Rebuild()
    {
        _body.RemoveAllChildren();
        if (_state is not { } state)
            return;

        if (state.Counts.Length > 1)
            _body.AddChild(Selector(Loc.GetString("terror-spider-choice-count"), state.Counts, _count, v => _count = v));

        if (state.Percents.Length > 1)
            _body.AddChild(Selector(Loc.GetString("terror-spider-choice-stillborn"), state.Percents, _percent, v => _percent = v, "%"));

        foreach (var option in state.Options)
        {
            var key = option.Key;
            var button = new Button { Text = option.Label, HorizontalExpand = true };
            button.OnPressed += _ =>
            {
                Chosen?.Invoke(state.Mode, key, _count, _percent);
                Close();
            };
            _body.AddChild(button);
        }
    }

    private Control Selector(string label, int[] values, int selected, Action<int> set, string suffix = "")
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        row.AddChild(new Label { Text = label });
        foreach (var value in values)
        {
            var v = value;
            var button = new Button { Text = $"{value}{suffix}", ToggleMode = true, Pressed = value == selected };
            button.OnPressed += _ =>
            {
                set(v);
                Rebuild();
            };
            row.AddChild(button);
        }

        return row;
    }
}
