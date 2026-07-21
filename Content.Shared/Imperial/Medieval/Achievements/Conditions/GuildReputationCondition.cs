using System.Linq;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class GuildReputationCondition : AchievementCondition
{
    [DataField(required: true)]
    public Dictionary<ProtoId<GuildTypePrototype>, float> RequiredReputations = new();

    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var msg = new FormattedMessage();
        msg.PushColor(Color.FromHex("#9e8c78"));

        var lines = new List<string>();

        foreach (var (guildType, req) in RequiredReputations)
        {
            if (!protoManager.TryIndex<GuildTypePrototype>(guildType, out var proto))
                continue;

            lines.Add(Loc.GetString("achievement-condition-guild-reputation",
                ("guild", proto.DisplayName), ("reputation", (int) req)));
        }

        msg.AddText(string.Join("\n", lines));

        msg.Pop();
        AppendRequirements(msg, protoManager);
        return msg;
    }

    public override bool Check(
        EntityUid player,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        object? context,
        Dictionary<string, int> progress)
    {
        if (!CheckFilters(player, entManager, protoManager))
            return false;

        foreach (var (guildType, required) in RequiredReputations)
        {
            if (progress.GetValueOrDefault($"{ProgressKey}:{guildType}", 0) < (int) required)
                return false;
        }

        return true;
    }

    public override bool TryUpdateProgress(
        EntityUid player,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        object? context,
        Dictionary<string, int> progress)
    {
        if (!CheckFilters(player, entManager, protoManager))
            return false;

        if (context is not GuildReputationContext ctx || !RequiredReputations.ContainsKey(ctx.GuildType))
            return false;

        var key = $"{ProgressKey}:{ctx.GuildType}";
        var next = (int) ctx.TotalReputation;

        if (next <= progress.GetValueOrDefault(key, 0))
            return false;

        progress[key] = next;
        return true;
    }
}

public record GuildReputationContext(ProtoId<GuildTypePrototype> GuildType, float TotalReputation);
