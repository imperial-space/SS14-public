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
