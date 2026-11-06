using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Lavaland.LavalandShuttle;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Lavaland.LavalandShuttle;

[UsedImplicitly]
public sealed class LavalandShuttleConsoleBoundUserInterface : BoundUserInterface
{
    private LavalandShuttleConsoleWindow? _window;

    public LavalandShuttleConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<LavalandShuttleConsoleWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is LavalandShuttleConsoleBoundUserInterfaceState s)
            _window?.UpdateState(s);
    }
}
