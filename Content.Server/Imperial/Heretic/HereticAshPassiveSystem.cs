using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Imperial.Lavaland.Storm;
using Content.Shared.Atmos.Components;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Temperature.Components;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticAshPassiveSystem : EntitySystem
{
    [Dependency] private readonly FlammableSystem _flammable = default!;

    private const float AshHeatThreshold = 100000f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticComponent, FlammableComponent>();
        while (query.MoveNext(out var uid, out var heretic, out var flammable))
        {
            if (heretic.CurrentPath != HereticPath.Ash || heretic.PassiveLevel < 2)
                continue;

            if (flammable.OnFire || flammable.FireStacks > 0f)
                _flammable.Extinguish(uid, flammable);
        }
    }

    public void ApplyPassiveLevel1(EntityUid uid)
    {
        EnsureComp<LavalandStormImmuneComponent>(uid);

        if (TryComp<TemperatureDamageComponent>(uid, out var tempDmg))
            tempDmg.HeatDamageThreshold = AshHeatThreshold;
    }

    public void ApplyPassiveLevel3(EntityUid uid)
    {
        EnsureComp<PressureImmunityComponent>(uid);
    }
}
