using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Imperial.Fission;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Fission;

[UsedImplicitly]
public sealed class FissionMonitorBoundUserInterface : BoundUserInterface
{
    private FissionMonitorWindow? _window;

    public FissionMonitorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FissionMonitorWindow>();
        _window.OnThrottle += value => SendMessage(new FissionSetThrottleMessage(value));
        _window.OnVent += () => SendMessage(new FissionToggleVentMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FissionMonitorUiState monitor)
            _window?.UpdateState(monitor);
    }
}
