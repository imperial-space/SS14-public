using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Content.Server.Imperial.EnsureCompAfterDelay.Components;

namespace Content.Server.Imperial.EnsureCompAfterDelay;

public sealed class EnsureCompAfterDelaySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnsureCompAfterDelayComponent, ComponentStartup>(GetDelayTime);
    }

    private void EnsureComponents(EntityUid uid, EnsureCompAfterDelayComponent component)
    {
        EntityManager.AddComponents(uid, component.Components);
        RemComp<EnsureCompAfterDelayComponent>(uid);
    }
    private void GetDelayTime(EntityUid uid, EnsureCompAfterDelayComponent component, ref ComponentStartup args)
    {
        component.DelayTime = component.DelayTime + _timing.CurTime;
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EnsureCompAfterDelayComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.DelayTime)
                EnsureComponents(uid, comp);
        }
    }
}
