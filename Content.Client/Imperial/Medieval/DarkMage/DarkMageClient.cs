using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.DarkMage;

public sealed partial class DarkMageAddOverlay : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    private DarkMageOverlay _overlay = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkMageAddOverlayComponent, ComponentInit>(ComponentStartup);
        SubscribeLocalEvent<DarkMageAddOverlayComponent, ComponentShutdown>(ComponentShutdown);
    }
    private void ComponentStartup(EntityUid uid, DarkMageAddOverlayComponent component, ComponentInit args)
    {
        _overlay = new(_gameTiming.CurTime);
        _overlayManager.AddOverlay(_overlay);
    }
    private void ComponentShutdown(EntityUid uid, DarkMageAddOverlayComponent component, ComponentShutdown args)
    {
        _overlayManager.RemoveOverlay(_overlay);
    }
}
