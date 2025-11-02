using Content.Shared.Imperial.HalloweenCultist.Components;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Content.Shared.CombatMode.Pacification;

namespace Content.Shared.Imperial.HalloweenCultist;

public class CursePacifySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CursePacifyComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<CursePacifyComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CursePacifyComponent>();
        while (query.MoveNext(out var uid, out var blind))
        {
            if (_timing.CurTime >= blind.Time)
                RemComp<CursePacifyComponent>(uid);
        }
    }
    private void OnStartup(Entity<CursePacifyComponent> ent, ref MapInitEvent args)
    {
        AddComp<PacifiedComponent>(ent);
        ent.Comp.Time += _timing.CurTime;
    }
    private void OnShutdown(Entity<CursePacifyComponent> ent, ref ComponentShutdown args)
    {
        RemComp<PacifiedComponent>(ent);
    }
}