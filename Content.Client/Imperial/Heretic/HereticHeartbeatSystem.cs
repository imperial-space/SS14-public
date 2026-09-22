using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticHeartbeatSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMgr = default!;

    private HereticHeartbeatArrowOverlay? _overlay;

    public void ShowArrow(EntityUid target)
    {
        if (_overlay == null)
        {
            _overlay = new HereticHeartbeatArrowOverlay();
            _overlayMgr.AddOverlay(_overlay);
        }
        _overlay.Target = target;
        _overlay.Show();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay == null)
            return;
        _overlayMgr.RemoveOverlay(_overlay);
        _overlay = null;
    }
}
