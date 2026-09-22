using Content.Shared.Objectives.Components;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticMajorSacrificeConditionSystem : EntitySystem
{
    [Dependency] private readonly HereticSystem _heretic = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMajorSacrificeConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, HereticMajorSacrificeConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        var count = _heretic.GetHighValueSacrificeCount(args.MindId);
        args.Progress = Math.Clamp((float) count / comp.RequiredSacrifices, 0f, 1f);
    }
}
