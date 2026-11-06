using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

public sealed class CadiaWarHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private CadiaWarHudOverlay? _hud;

    public override void Initialize()
    {
        base.Initialize();
        _hud = new CadiaWarHudOverlay();
        _overlay.AddOverlay(_hud);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_hud != null)
            _overlay.RemoveOverlay(_hud);
    }
}
