using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Items;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Moon.Parade;
using Content.Server.Imperial.Heretic.Paths.Void;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic.Paths.Moon;

/// <summary>
/// status_effect/heretic_passive/moon: лечит мозг, на 2 уровне не даёт уснуть,
/// на 3 уровне заклинания сами проводят эффект лунного амулета.
/// </summary>
public sealed class HereticMoonPassiveSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    /// <summary>tick(): 1 единица на уровень пассивки в секунду.</summary>
    private const float HealPerLevel = 1f;

    /// <summary>heretic_datum.ascended → healing_amount = -15.</summary>
    private const float AscendedHeal = 15f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMoonPassiveComponent, TryingToSleepEvent>(OnTryingSleep);
        SubscribeLocalEvent<HereticMoonPassiveComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnTryingSleep(EntityUid uid, HereticMoonPassiveComponent comp, ref TryingToSleepEvent args)
    {
        if (!TryComp<HereticComponent>(uid, out var heretic))
            return;
        if (heretic.CurrentPath != HereticPath.Moon || heretic.PassiveLevel < 2)
            return;
        args.Cancelled = true;
    }

    private void OnDamageChanged(EntityUid uid, HereticMoonPassiveComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;
        comp.TimeOfLastDamage = _gameTiming.CurTime;
    }

    public void ApplyPassiveLevel1(EntityUid uid)
    {
        EnsureComp<HereticMoonPassiveComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<HereticComponent, HereticMoonBrainDamageComponent>();
        while (query.MoveNext(out var uid, out var heretic, out var brain))
        {
            if (heretic.CurrentPath != HereticPath.Moon) continue;
            if (heretic.PassiveLevel < 1) continue;
            if (brain.BrainDamage <= 0f) continue;

            var rate = heretic.AscensionTriggered
                ? AscendedHeal
                : HealPerLevel * heretic.PassiveLevel;

            // Без лунного амулета восстановление вдвое медленнее.
            if (!HasComp<HereticMoonAmuletEquippedComponent>(uid))
                rate *= 0.5f;

            // В бою — тоже вдвое медленнее.
            if (TryComp<HereticMoonPassiveComponent>(uid, out var passive) && passive.IsInCombat(curTime))
                rate *= 0.5f;

            brain.BrainDamage = Math.Max(0f, brain.BrainDamage - rate * frameTime);
        }
    }
}
