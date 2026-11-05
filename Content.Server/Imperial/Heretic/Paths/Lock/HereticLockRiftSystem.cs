using Content.Shared.Imperial.Heretic.Paths.Lock;

namespace Content.Server.Imperial.Heretic.Paths.Lock;

public sealed class HereticLockRiftSystem : EntitySystem
{
    [Dependency] private readonly Robust.Shared.Timing.IGameTiming _timing = default!;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticLockRiftComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextSpawn)
                continue;

            comp.NextSpawn = _timing.CurTime + TimeSpan.FromSeconds(comp.SpawnInterval);
            Spawn(comp.SpawnEntity, Transform(uid).Coordinates);
        }
    }
}
