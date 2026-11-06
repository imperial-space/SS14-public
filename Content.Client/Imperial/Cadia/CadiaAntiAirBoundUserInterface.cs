using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

public sealed class CadiaAntiAirBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CadiaAntiAirWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CadiaAntiAirWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnAim += offset => SendMessage(new CadiaAntiAirAimMessage(offset));
        _window.OnLevel += level => SendMessage(new CadiaAntiAirLevelMessage(level));
        _window.OnFire += firing => SendMessage(new CadiaAntiAirFireMessage(firing));
        _window.OnView += grid => SendMessage(new CadiaAntiAirViewMessage(grid));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CadiaAntiAirBuiState aa)
            _window?.Update(aa);
    }
}
