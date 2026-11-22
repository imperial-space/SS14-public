using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.SyndieBattle.Components;
using Content.Server.Power.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Station.Components;

namespace Content.Server.Imperial.SyndieBattle.Systems;

public sealed class SyndieBattlePowerSystem : GameRuleSystem<SyndieBattlePowerComponent>
{
    [Dependency] private readonly SharedBatterySystem _batterySystem = default!;

    protected override void Started(EntityUid uid, SyndieBattlePowerComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        component.NextRecharge = Timing.CurTime;
        component.Stations.Clear();
    }

    protected override void Ended(EntityUid uid, SyndieBattlePowerComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        component.Stations.Clear();
    }

    protected override void ActiveTick(EntityUid uid, SyndieBattlePowerComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (Timing.CurTime < component.NextRecharge)
            return;

        component.NextRecharge = Timing.CurTime + component.RechargeInterval;

        var query = EntityQueryEnumerator<BatteryComponent, PowerNetworkBatteryComponent, TransformComponent>();
        while (query.MoveNext(out var batteryUid, out var battery, out _, out var transform))
        {
            if (transform.GridUid is not { } grid ||
                !TryComp<StationMemberComponent>(grid, out var stationMember) ||
                !component.Stations.Contains(stationMember.Station))
                continue;

            if (_batterySystem.GetCharge((batteryUid, battery)) >= battery.MaxCharge)
                continue;

            _batterySystem.SetCharge((batteryUid, battery), battery.MaxCharge);
        }
    }
}
