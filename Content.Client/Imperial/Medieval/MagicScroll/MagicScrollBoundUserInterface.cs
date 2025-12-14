using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Robust.Client.UserInterface;

//=========================================================================
// MagicScrollBoundUserInterface.cs
//=========================================================================
// Purpose: Client-side bound UI for magic scroll interactions
// Author: rhailrake
//=========================================================================

namespace Content.Client.Imperial.Medieval.MagicScroll;

public sealed class MagicScrollBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private MagicScrollWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MagicScrollWindow>();
        _window.Owner = this;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not MagicScrollBoundUserInterfaceState castState)
            return;

        _window.UpdateState(castState);
    }
}
