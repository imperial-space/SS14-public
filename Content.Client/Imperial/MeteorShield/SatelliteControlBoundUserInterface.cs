using System.Numerics;
using Content.Shared.Imperial.MeteorShield;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.MeteorShield;

[UsedImplicitly]
public sealed class SatelliteControlBoundUserInterface : BoundUserInterface
{
    private SatelliteControlWindow? _window;

    public SatelliteControlBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SatelliteControlWindow>();
        _window.OnToggle += id => SendMessage(new SatelliteToggleMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SatelliteControlUiState sat)
            _window?.UpdateState(sat);
    }
}
