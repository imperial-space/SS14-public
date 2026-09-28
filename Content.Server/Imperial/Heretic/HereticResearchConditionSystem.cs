using Content.Shared.Objectives.Components;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticResearchConditionSystem : EntitySystem
{
    [Dependency] private readonly HereticSystem _heretic = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticResearchConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, HereticResearchConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        var count = _heretic.GetResearchedKnowledgeCount(args.MindId);
        args.Progress = Math.Clamp((float) count / comp.RequiredKnowledge, 0f, 1f);
    }
}
