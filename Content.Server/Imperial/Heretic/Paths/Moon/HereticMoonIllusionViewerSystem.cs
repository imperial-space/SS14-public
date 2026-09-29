using Content.Shared.Eye;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Heretic.Paths.Moon;

public sealed class HereticMoonIllusionViewerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMoonIllusionViewerComponent, GetVisMaskEvent>(OnGetVisMask);
    }

    private void OnGetVisMask(EntityUid uid, HereticMoonIllusionViewerComponent _, ref GetVisMaskEvent args)
        => args.VisibilityMask |= (int) VisibilityFlags.HereticIllusion;
}
