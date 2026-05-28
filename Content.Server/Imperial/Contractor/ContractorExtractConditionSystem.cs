using Content.Server.Objectives.Systems;
using Content.Shared.Imperial.Contractor.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server.Imperial.Contractor;

public sealed class ContractorExtractConditionSystem : EntitySystem
{
    [Dependency] private readonly ContractorSystem _contractor = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContractorExtractConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, ContractorExtractConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (!_target.GetTarget(uid, out var targetMind))
            return;

        args.Progress = _contractor.HasCompletedAliveExtraction(targetMind.Value) ? 1f : 0f;
    }
}