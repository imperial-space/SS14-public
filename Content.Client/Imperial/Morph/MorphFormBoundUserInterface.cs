using System.Numerics;
using Content.Shared.Imperial.Morph;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Morph;

/// <summary>tgui_input_list «Which form do you want to become?».</summary>
public sealed class MorphFormBoundUserInterface : BoundUserInterface
{
    private MorphFormWindow? _window;

    public MorphFormBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<MorphFormWindow>();
        _window.Chosen += index => SendMessage(new MorphFormChosenMessage(index));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is MorphFormBoundUserInterfaceState forms)
            _window?.UpdateState(forms);
    }
}

public sealed class MorphFormWindow : DefaultWindow
{
    public event Action<int>? Chosen;

    private readonly BoxContainer _body = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };

    public MorphFormWindow()
    {
        Title = Loc.GetString("morph-form-window-title");
        MinSize = new Vector2(260, 120);
        SetSize = new Vector2(280, 260);
        Contents.AddChild(new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            Children = { _body },
        });
    }

    public void UpdateState(MorphFormBoundUserInterfaceState state)
    {
        _body.RemoveAllChildren();

        if (state.CanRestore)
            AddOption(Loc.GetString("morph-form-original"), MorphFormChosenMessage.OriginalForm);

        for (var i = 0; i < state.Forms.Count; i++)
        {
            AddOption(state.Forms[i], i);
        }
    }

    private void AddOption(string text, int index)
    {
        var button = new Button { Text = text, HorizontalExpand = true, ClipText = true };
        button.OnPressed += _ =>
        {
            Chosen?.Invoke(index);
            Close();
        };
        _body.AddChild(button);
    }
}
