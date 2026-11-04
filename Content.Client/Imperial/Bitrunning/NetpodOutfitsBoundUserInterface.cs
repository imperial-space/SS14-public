using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Bitrunning;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Bitrunning;

[UsedImplicitly]
public sealed class NetpodOutfitsBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private NetpodOutfitsWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<NetpodOutfitsWindow>();
        _window.OnSelect += id => SendMessage(new NetpodSelectOutfitMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is NetpodOutfitState outfits)
            _window?.Update(outfits);
    }
}
