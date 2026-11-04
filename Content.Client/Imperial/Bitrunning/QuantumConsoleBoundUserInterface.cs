using System.Linq;
using System.Numerics;
using System.Text;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Bitrunning;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Bitrunning;

[UsedImplicitly]
public sealed class QuantumConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private QuantumConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<QuantumConsoleWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is QuantumConsoleState console)
            _window?.Update(console);
    }
}
