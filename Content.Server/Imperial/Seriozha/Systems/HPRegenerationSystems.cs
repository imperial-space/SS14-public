using Content.Shared.Damage;
using Content.Shared.Imperial.Seriozha.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Seriozha.Systems;

public sealed partial class HPRegenerationSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HPRegenerationComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<HPRegenerationComponent, ComponentStartup>(OnStartup);
    }

    private void OnInit(EntityUid uid, HPRegenerationComponent component, MapInitEvent args)
    {
        component.NextRegenTime = _timing.CurTime + TimeSpan.FromSeconds(component.SecondInterval);
        component.SecondInterval = MathF.Max(0.1f, component.SecondInterval);
        component.NextRegenTime = _timing.CurTime + TimeSpan.FromSeconds(component.SecondInterval);
    }

    private void OnStartup(EntityUid uid, HPRegenerationComponent component, ComponentStartup args)
    {
        component.SecondInterval = MathF.Max(0.1f, component.SecondInterval);
        component.NextRegenTime = _timing.CurTime + TimeSpan.FromSeconds(component.SecondInterval);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<HPRegenerationComponent>();
        while (query.MoveNext(out var uid, out var regenComp))
        {
            if (regenComp.SecondInterval <= 0f)
                continue;
            var step = TimeSpan.FromSeconds(regenComp.SecondInterval);
            if (curTime < regenComp.NextRegenTime)
                continue;

            if (_mobState.IsDead(uid))
                continue;

            do
            {
                _damageable.TryChangeDamage(uid, regenComp.RegenerationAmount, true);
                regenComp.NextRegenTime += step;
            } while (curTime >= regenComp.NextRegenTime);
        }
    }
}
