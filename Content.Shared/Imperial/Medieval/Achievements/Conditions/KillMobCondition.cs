using System.Linq;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class KillMobCondition : AchievementCondition
{
    [DataField(required: true)]
    public List<string> TargetMobPrototypes = new();

    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var msg = new FormattedMessage();
        msg.PushColor(Color.FromHex("#9e8c78"));

        var mobNames = TargetMobPrototypes
            .Select(id => protoManager.TryIndex<EntityPrototype>(id, out var ep) ? ep.Name : id)
            .Distinct()
            .ToList();

        if (mobNames.Count > 1)
        {
            msg.AddText(Loc.GetString("achievement-condition-kill-multi-header") + "\n");
            foreach (var name in mobNames)
            {
                msg.AddText($"  • {name}\n");
            }
            
            if (RequiredCount > 1)
                msg.AddText(Loc.GetString("achievement-condition-kill-count", ("count", RequiredCount)));
        }
        else
        {
            var target = mobNames.FirstOrDefault() ?? Loc.GetString("achievement-condition-any-mob");
            if (RequiredCount == 1)
                msg.AddText(Loc.GetString("achievement-condition-kill-single", ("target", target)));
            else
                msg.AddText(Loc.GetString("achievement-condition-kill-mob", ("count", RequiredCount), ("targets", target)));
        }

        msg.Pop();
        AppendRequirements(msg, protoManager);
        return msg;
    }

    public override bool Check(EntityUid player, IEntityManager entManager, IPrototypeManager protoManager, object? context, Dictionary<string, int> progress)
    {
        return CheckFilters(player, entManager, protoManager) && progress.GetValueOrDefault(ProgressKey, 0) >= RequiredCount;
    }

    public override bool TryUpdateProgress(EntityUid player, IEntityManager entManager, IPrototypeManager protoManager, object? context, Dictionary<string, int> progress)
    {
        if (!CheckFilters(player, entManager, protoManager) || context is not EntityUid deadMob)
            return false;

        var meta = entManager.GetComponent<MetaDataComponent>(deadMob);
        if (meta.EntityPrototype == null || !TargetMobPrototypes.Contains(meta.EntityPrototype.ID))
            return false;

        progress[ProgressKey] = progress.GetValueOrDefault(ProgressKey, 0) + 1;
        return true;
    }
}
