using Content.Shared.Objectives.Components;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticSacrificeConditionSystem : EntitySystem
{
    [Dependency] private readonly HereticSystem _heretic = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticSacrificeConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, HereticSacrificeConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        var count = _heretic.GetSacrificeCount(args.MindId);
        args.Progress = Math.Clamp((float) count / comp.RequiredSacrifices, 0f, 1f);
    }
}
