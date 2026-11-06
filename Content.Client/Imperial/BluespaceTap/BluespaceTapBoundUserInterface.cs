using System.Globalization;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.BluespaceTap;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.BluespaceTap;

[UsedImplicitly]
public sealed class BluespaceTapBoundUserInterface : BoundUserInterface
{
    private BluespaceTapWindow? _window;

    public BluespaceTapBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BluespaceTapWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BluespaceTapUiState tap)
            _window?.UpdateState(tap);
    }
}
