using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Player;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class KillFactionPlayerCondition : AchievementCondition
{
    [DataField]
    public List<ProtoId<MedievalFactionPrototype>> TargetFactions = new();

    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var msg = new FormattedMessage();
        msg.PushColor(Color.FromHex("#9e8c78"));

        if (TargetFactions.Count == 0)
        {
            msg.AddText(Loc.GetString("achievement-condition-kill-faction-any",
                ("count", RequiredCount)));
        }
        else if (TargetFactions.Count == 1)
        {
            var factionId = TargetFactions[0];
            var (name, color) = GetFactionNameAndColor(factionId, protoManager);

            msg.Pop();
            msg.AddText(Loc.GetString("achievement-condition-kill-faction-single-prefix",
                ("count", RequiredCount)) + " ");
            msg.PushColor(color);
            msg.AddText(name);
            msg.Pop();
        }
        else
        {
            msg.AddText(Loc.GetString("achievement-condition-kill-faction-multi-header",
                ("count", RequiredCount)) + "\n");
            msg.Pop();

            foreach (var factionId in TargetFactions)
            {
                var (name, color) = GetFactionNameAndColor(factionId, protoManager);
                msg.AddText("  • ");
                msg.PushColor(color);
                msg.AddText(name);
                msg.Pop();
                msg.AddText("\n");
            }
        }

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
        return CheckFilters(player, entManager, protoManager)
               && progress.GetValueOrDefault(ProgressKey, 0) >= RequiredCount;
    }

    public override bool TryUpdateProgress(
        EntityUid player,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        object? context,
        Dictionary<string, int> progress)
    {
        if (!CheckFilters(player, entManager, protoManager) || context is not EntityUid victim)
            return false;

        if (!entManager.HasComponent<ActorComponent>(victim))
            return false;

        if (TargetFactions.Count > 0)
        {
            if (!entManager.TryGetComponent<MedievalFactionMemberComponent>(victim, out var factionMember))
                return false;

            if (!TargetFactions.Contains(factionMember.Faction))
                return false;
        }

        if (victim == player)
            return false;

        progress[ProgressKey] = progress.GetValueOrDefault(ProgressKey, 0) + 1;
        return true;
    }


    private static (string name, Color color) GetFactionNameAndColor(
        ProtoId<MedievalFactionPrototype> factionId,
        IPrototypeManager protoManager)
    {
        if (protoManager.TryIndex<MedievalFactionPrototype>(factionId, out var proto))
            return (Loc.GetString(proto.Name), proto.Color);

        return (factionId.ToString(), Color.White);
    }
}
