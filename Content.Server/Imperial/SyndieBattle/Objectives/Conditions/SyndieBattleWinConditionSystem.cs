using Content.Shared.Objectives.Components;

namespace Content.Server.Imperial.SyndieBattle.Objectives.Conditions;

public sealed class SyndieBattleWinConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SyndieBattleWinConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, SyndieBattleWinConditionComponent component, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = args.Mind.UserId is { } userId && TryGetActiveRule(out var rule) && rule.Winner == userId ? 1f : 0f;
    }

    private bool TryGetActiveRule(out SyndieBattleRuleComponent rule)
    {
        var query = EntityQueryEnumerator<SyndieBattleRuleComponent>();
        while (query.MoveNext(out _, out var candidate))
        {
            if (!candidate.Active)
                continue;

            rule = candidate;
            return true;
        }

        rule = default!;
        return false;
    }
}
