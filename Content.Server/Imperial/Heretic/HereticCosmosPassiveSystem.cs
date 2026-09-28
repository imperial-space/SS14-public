using Content.Server.Imperial.Heretic.Components;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticCosmosPassiveSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform      = default!;
    [Dependency] private readonly EntityLookupSystem    _lookup     = default!;
    [Dependency] private readonly TriggerSystem         _trigger    = default!;
    [Dependency] private readonly IGameTiming           _gameTiming = default!;

    private TimeSpan _nextGrenadeCheck;
    private static readonly TimeSpan GrenadeCheckInterval = TimeSpan.FromSeconds(0.5);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TimerTriggerComponent, AttemptTriggerEvent>(OnAttemptGrenadeActivate);
    }

    public void ApplyPassiveLevel1(EntityUid uid) => EnsureComp<HereticCosmosPassiveComponent>(uid);
    public void ApplyPassiveLevel2(EntityUid uid) => EnsureComp<HereticCosmosPassiveComponent>(uid);
    public void ApplyPassiveLevel3(EntityUid uid) => EnsureComp<HereticCosmosPassiveComponent>(uid);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _gameTiming.CurTime;
        if (now < _nextGrenadeCheck)
            return;
        _nextGrenadeCheck = now + GrenadeCheckInterval;

        var query = EntityQueryEnumerator<ActiveTimerTriggerComponent, TimerTriggerComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (IsInCosmicField(uid, 2))
                _trigger.StopTimerTrigger(uid);
        }
    }

    private void OnAttemptGrenadeActivate(EntityUid uid, TimerTriggerComponent _, ref AttemptTriggerEvent args)
    {
        if (IsInCosmicField(uid, 2))
            args.Cancelled = true;
    }

    private bool IsInCosmicField(EntityUid uid, int minLevel)
    {
        var mapCoords = _xform.GetMapCoordinates(uid);
        foreach (var field in _lookup.GetEntitiesInRange<HereticCosmicFieldComponent>(mapCoords, 0.6f))
        {
            if (field.Comp.PassiveLevel >= minLevel)
                return true;
        }
        return false;
    }
}
