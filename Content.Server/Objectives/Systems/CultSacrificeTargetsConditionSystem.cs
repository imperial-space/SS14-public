using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.Cult.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles.Jobs;

namespace Content.Server.Objectives.Systems;

public sealed class CultSacrificeTargetsConditionSystem : EntitySystem
{
    [Dependency] private readonly CultRuleSystem _cultRule = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultSacrificeTargetsConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<CultSacrificeTargetsConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnAfterAssign(EntityUid uid, CultSacrificeTargetsConditionComponent comp, ref ObjectiveAfterAssignEvent args)
    {
        var targets = _cultRule.GetSacrificeTargets();

        _metaData.SetEntityDescription(uid,
            Loc.GetString("cult-objective-sacrifice-targets-desc"),
            args.Meta);

        if (targets.Count == 0)
        {
            _metaData.SetEntityName(uid,
                Loc.GetString("cult-objective-sacrifice-targets-none-name"),
                args.Meta);
            return;
        }

        if (targets.Count == 1)
        {
            _metaData.SetEntityName(uid,
                Loc.GetString("cult-objective-sacrifice-targets-single-name",
                    ("target1", GetTargetName(targets[0])),
                    ("job1", GetTargetJobName(targets[0]))),
                args.Meta);
            return;
        }

        _metaData.SetEntityName(uid,
            Loc.GetString("cult-objective-sacrifice-targets-name",
                ("target1", GetTargetName(targets[0])),
                ("job1", GetTargetJobName(targets[0])),
                ("target2", GetTargetName(targets[1])),
                ("job2", GetTargetJobName(targets[1]))),
            args.Meta);
    }

    private void OnGetProgress(EntityUid uid, CultSacrificeTargetsConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = _cultRule.GetSacrificeProgress();
    }

    private string GetTargetName(EntityUid mindUid)
    {
        return TryComp<MindComponent>(mindUid, out var mind) && !string.IsNullOrWhiteSpace(mind.CharacterName)
            ? mind.CharacterName
            : Loc.GetString("cult-objective-sacrifice-targets-fallback-name");
    }

    private string GetTargetJobName(EntityUid mindUid)
    {
        var jobName = _job.MindTryGetJobName(mindUid);
        return string.IsNullOrWhiteSpace(jobName)
            ? Loc.GetString("cult-objective-sacrifice-targets-fallback-job")
            : jobName;
    }
}