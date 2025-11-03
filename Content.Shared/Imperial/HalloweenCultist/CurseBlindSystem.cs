using Content.Shared.Imperial.HalloweenCultist.Components;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Content.Shared.Eye.Blinding.Components;

namespace Content.Shared.Imperial.HalloweenCultist;

public class CurseBlindSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CurseBlindComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<CurseBlindComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CurseBlindComponent>();
        while (query.MoveNext(out var uid, out var blind))
        {
            if (_timing.CurTime >= blind.Time)
                RemComp<CurseBlindComponent>(uid);
        }
    }
    private void OnStartup(Entity<CurseBlindComponent> ent, ref MapInitEvent args)
    {
        AddComp<TemporaryBlindnessComponent>(ent);
        ent.Comp.Time += _timing.CurTime;
    }
    private void OnShutdown(Entity<CurseBlindComponent> ent, ref ComponentShutdown args)
    {
        RemComp<TemporaryBlindnessComponent>(ent);
    }
}