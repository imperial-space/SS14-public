using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticStarTouchBeamVisualizerSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new HereticStarTouchBeamOverlay(
            EntityManager,
            _timing,
            IoCManager.Resolve<IResourceCache>(),
            IoCManager.Resolve<IPrototypeManager>()
        ));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<HereticStarTouchBeamOverlay>();
    }
}
