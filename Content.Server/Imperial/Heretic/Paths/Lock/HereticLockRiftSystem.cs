using Content.Shared.Imperial.Heretic.Paths.Lock;

namespace Content.Server.Imperial.Heretic.Paths.Lock;

public sealed class HereticLockRiftSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticLockRiftComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.Timer += frameTime;
            if (comp.Timer < comp.SpawnInterval)
                continue;

            comp.Timer = 0f;
            Spawn(comp.SpawnEntity, Transform(uid).Coordinates);
        }
    }
}
