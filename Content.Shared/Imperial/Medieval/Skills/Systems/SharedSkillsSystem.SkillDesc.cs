using Content.Shared.Examine;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    private void InitializeDesc()
    {
        SubscribeLocalEvent<SkillDescriptionComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, SkillDescriptionComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var (_, level) = GetSkill(args.Examiner, component.SkillId);

        if (component.Level > 10 ? component.Level > level : component.Level < level)
            return;

        args.PushMarkup(component.Desc);
    }
}
