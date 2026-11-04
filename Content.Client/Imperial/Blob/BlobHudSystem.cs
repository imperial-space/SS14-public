using System.Numerics;
using Content.Shared.Imperial.Blob.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Blob;

public sealed class BlobHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private BlobHudOverlay? _hud;

    public override void Initialize()
    {
        base.Initialize();
        _hud = new BlobHudOverlay();
        _overlay.AddOverlay(_hud);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_hud != null)
            _overlay.RemoveOverlay(_hud);
    }
}
