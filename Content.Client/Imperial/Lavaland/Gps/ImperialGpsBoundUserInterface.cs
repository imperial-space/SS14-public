using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Lavaland.Gps;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Lavaland.Gps;

[UsedImplicitly]
public sealed class ImperialGpsBoundUserInterface : BoundUserInterface
{
    private ImperialGpsWindow? _window;

    public ImperialGpsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ImperialGpsWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ImperialGpsUiState gps)
            _window?.UpdateState(gps);
    }
}
