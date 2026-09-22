using Content.Shared.Objectives.Components;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticSummonConditionSystem : EntitySystem
{
    [Dependency] private readonly HereticSystem _heretic = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticSummonConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, HereticSummonConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        var count = _heretic.GetSummonCount(args.MindId);
        args.Progress = Math.Clamp((float) count / comp.RequiredSummons, 0f, 1f);
    }
}
