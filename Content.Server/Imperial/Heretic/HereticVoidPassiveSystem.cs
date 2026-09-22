using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Shared.Slippery;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticVoidPassiveSystem : EntitySystem
{
    private const float ColdImmunityThreshold = 0f;

    public void ApplyPassiveLevel1(EntityUid uid)
    {
        EnsureComp<PressureImmunityComponent>(uid);

        if (TryComp<TemperatureDamageComponent>(uid, out var tempDmg))
            tempDmg.ColdDamageThreshold = ColdImmunityThreshold;
    }

    public void ApplyPassiveLevel2(EntityUid uid)
    {
        RemComp<RespiratorComponent>(uid);
    }

    public void ApplyPassiveLevel3(EntityUid uid)
    {
        EnsureComp<NoSlipComponent>(uid);
    }
}
