using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Handles the Rusting Crown mark DoT: applies 5 Caustic damage every 5 seconds for 30 seconds.
/// </summary>
public sealed class HereticRustingCrownMarkSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage  = default!;
    [Dependency] private readonly IGameTiming      _timing  = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HereticRustingCrownMarkComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var mark, out _))
        {
            if (now < mark.NextTick) continue;

            var dmg = new DamageSpecifier();
            dmg.DamageDict["Caustic"] = FixedPoint2.New(mark.DamagePerTick);
            _damage.TryChangeDamage(uid, dmg, ignoreResistances: false);

            mark.TicksRemaining--;
            if (mark.TicksRemaining <= 0)
                RemCompDeferred<HereticRustingCrownMarkComponent>(uid);
            else
                mark.NextTick = now + TimeSpan.FromSeconds(5);
        }
    }
}
