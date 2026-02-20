using Content.Server.Imperial.SCP.SCP008.Components;
using Content.Server.Zombies;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Zombies;
using Robust.Shared.Timing;
using Robust.Shared.Localization;

namespace Content.Server.Imperial.SCP.SCP008.Systems;

public sealed class SCP008InfectionAuraSystem : EntitySystem
{
    private readonly HashSet<EntityUid> _reusableInRange = new();
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ZombieSystem _zombie = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SCP008InfectionAuraComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SCP008InfectionAuraComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<SCP008InfectionAuraComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.LastUpdate = _timing.CurTime;
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateInterval;
        ent.Comp.ExposureTime.Clear();
    }

    private void OnShutdown(Entity<SCP008InfectionAuraComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.ExposureTime.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SCP008InfectionAuraComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextUpdate > curTime)
                continue;

            var elapsed = curTime - comp.LastUpdate;
            comp.LastUpdate = curTime;
            comp.NextUpdate = curTime + comp.UpdateInterval;

            if (elapsed <= TimeSpan.Zero)
                continue;

            _reusableInRange.Clear();
            var nearbyEntities = _lookup.GetEntitiesInRange(uid, comp.Radius, comp.LookupFlags);

            foreach (var target in nearbyEntities)
            {
                if (target == uid)
                    continue;

                if (!TryComp<MobStateComponent>(target, out var mobState))
                    continue;

                var validState = mobState.CurrentState == MobState.Alive ||
                                 (comp.AllowCritical && mobState.CurrentState == MobState.Critical);

                if (!validState)
                    continue;

                if (HasComp<ZombieComponent>(target) || HasComp<ZombieImmuneComponent>(target))
                    continue;

                _reusableInRange.Add(target);

                var totalExposure = elapsed;
                var previousExposure = TimeSpan.Zero;
                if (comp.ExposureTime.TryGetValue(target, out var existingExposure))
                {
                    previousExposure = existingExposure;
                    totalExposure += previousExposure;
                }

                var warningStart = comp.ZombifyDelay - comp.WarningDelay;
                if (warningStart < TimeSpan.Zero)
                    warningStart = TimeSpan.Zero;

                if (previousExposure < warningStart && totalExposure >= warningStart && totalExposure < comp.ZombifyDelay)
                    _popup.PopupEntity(Loc.GetString(comp.WarningPopup), target, target, PopupType.MediumCaution);

                if (totalExposure >= comp.ZombifyDelay)
                {
                    _zombie.ZombifyEntity(target, mobState);
                    comp.ExposureTime.Remove(target);
                    continue;
                }

                comp.ExposureTime[target] = totalExposure;
            }

            var toRemove = new List<EntityUid>();
            foreach (var (tracked, _) in comp.ExposureTime)
            {
                if (!_reusableInRange.Contains(tracked))
                    toRemove.Add(tracked);
            }

            foreach (var tracked in toRemove)
            {
                comp.ExposureTime.Remove(tracked);
            }
        }
    }
}
