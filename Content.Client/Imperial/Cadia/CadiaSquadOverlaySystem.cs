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

public sealed class CadiaSquadOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    private CadiaSquadOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new CadiaSquadOverlay();
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }
}
