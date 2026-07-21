using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class CapturePointCondition : AchievementCondition
{
    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var msg = new FormattedMessage();
        msg.PushColor(Color.FromHex("#9e8c78"));
        
        msg.AddText(Loc.GetString("achievement-condition-capture-point", 
            ("count", RequiredCount)));

        msg.Pop();
        AppendRequirements(msg, protoManager);
        return msg;
    }

    public override bool Check(EntityUid player, IEntityManager entManager, IPrototypeManager protoManager, object? context, Dictionary<string, int> progress)
    {
        return CheckFilters(player, entManager, protoManager) && 
               progress.GetValueOrDefault(ProgressKey, 0) >= RequiredCount;
    }

    public override bool TryUpdateProgress(EntityUid player, IEntityManager entManager, IPrototypeManager protoManager, object? context, Dictionary<string, int> progress)
    {
        if (!CheckFilters(player, entManager, protoManager))
            return false;

        if (context is not CapturePointUpdateContext)
            return false;

        progress[ProgressKey] = progress.GetValueOrDefault(ProgressKey, 0) + 1;
        return true;
    }
}

public record CapturePointUpdateContext;
