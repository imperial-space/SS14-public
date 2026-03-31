using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Station.Systems;
using Content.Shared.Objectives.Components;

namespace Content.Server.Objectives.Systems;

public sealed class CultSummonNarSieConditionSystem : EntitySystem
{
    [Dependency] private readonly CultRuleSystem _cultRule = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultSummonNarSieConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<CultSummonNarSieConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnAfterAssign(EntityUid uid, CultSummonNarSieConditionComponent comp, ref ObjectiveAfterAssignEvent args)
    {
        var station = args.Mind.OwnedEntity is { } owned
            ? _station.GetOwningStation(owned)
            : null;

        var beacons = _cultRule.GetNarSieBeaconSummary(station);
        _metaData.SetEntityName(uid,
            Loc.GetString("cult-objective-summon-narsie-name-beacons",
                ("beacons", beacons)),
            args.Meta);
        _metaData.SetEntityDescription(uid,
            Loc.GetString("cult-objective-summon-narsie-desc"),
            args.Meta);
    }

    private void OnGetProgress(EntityUid uid, CultSummonNarSieConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = _cultRule.IsNarSieSummoned() ? 1f : 0f;
    }
}