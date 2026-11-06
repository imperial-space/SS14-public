using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.BSA;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.BSA;

[UsedImplicitly]
public sealed class BSABoundUserInterface : BoundUserInterface
{
    private BSAConsoleWindow? _window;

    public BSABoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BSAConsoleWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BSAConsoleUiState bsa)
            _window?.UpdateState(bsa);
    }
}
