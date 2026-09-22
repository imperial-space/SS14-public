using Content.Shared.Imperial.Lavaland;
using Content.Shared.Nutrition;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland;

public sealed class HolymelonLabyrinthPassSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan BuffDuration = TimeSpan.FromSeconds(60);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoodHolymelonComponent, IngestedEvent>(OnIngested);
    }

    private void OnIngested(Entity<FoodHolymelonComponent> food, ref IngestedEvent args)
    {
        var eater = args.Target;
        if (TerminatingOrDeleted(eater))
            return;

        var comp = EnsureComp<HolymelonLabyrinthPassComponent>(eater);
        comp.ExpiresAt = _timing.CurTime + BuffDuration;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HolymelonLabyrinthPassComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.ExpiresAt)
                RemComp<HolymelonLabyrinthPassComponent>(uid);
        }
    }
}
