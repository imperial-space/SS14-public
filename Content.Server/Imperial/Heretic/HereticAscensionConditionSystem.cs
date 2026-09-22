using Content.Shared.Objectives.Components;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticAscensionConditionSystem : EntitySystem
{
    [Dependency] private readonly HereticSystem _heretic = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticAscensionConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, HereticAscensionConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = _heretic.IsAscended(args.MindId) ? 1f : 0f;
    }
}
