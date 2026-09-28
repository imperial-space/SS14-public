using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticMoonPassiveSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMoonPassiveComponent, TryingToSleepEvent>(OnTryingSleep);
        SubscribeLocalEvent<HereticMoonPassiveComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnTryingSleep(EntityUid uid, HereticMoonPassiveComponent comp, ref TryingToSleepEvent args)
    {
        if (!TryComp<HereticComponent>(uid, out var heretic)) return;
        if (heretic.CurrentPath != HereticPath.Moon || heretic.PassiveLevel < 2) return;
        args.Cancelled = true;
    }

    private void OnDamageChanged(EntityUid uid, HereticMoonPassiveComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased) return;
        comp.TimeOfLastDamage = _gameTiming.CurTime;
    }

    public void ApplyPassiveLevel1(EntityUid uid)
    {
        EnsureComp<HereticMoonPassiveComponent>(uid);
    }
}
