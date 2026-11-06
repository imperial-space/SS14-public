using System.Linq;
using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

public sealed class CadiaSquadBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CadiaSquadWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CadiaSquadWindow>();
        _window.Title = Loc.GetString("cadia-squad-ui-title");
        _window.OnSend += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CadiaSquadState squads)
            _window?.Update(squads);
    }
}
