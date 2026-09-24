using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Tools.Components;

namespace Content.Shared.Imperial.Heretic;

public sealed partial class HereticCloakSlowSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticCloakActiveComponent, ToolGetDelayModifierEvent>(OnToolDelay);
    }

    private void OnToolDelay(EntityUid uid, HereticCloakActiveComponent comp, ref ToolGetDelayModifierEvent args)
    {
        args.DelayMultiplier *= 2f;
    }
}
