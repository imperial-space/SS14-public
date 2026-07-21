using System.Linq;
using Content.Shared._CP14.Workbench;
using Content.Shared._CP14.Workbench.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class CraftItemCondition : AchievementCondition
{
    [DataField]
    public List<EntProtoId> TargetItems = new();

    [DataField]
    public List<EntProtoId> RequiredWorkbenches = new();

    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var msg = new FormattedMessage();
        msg.PushColor(Color.FromHex("#9e8c78"));

        if (TargetItems.Count == 0)
        {
            msg.AddText(Loc.GetString("achievement-condition-craft-any",
                ("count", RequiredCount)));
        }
        else if (TargetItems.Count == 1)
        {
            var name = protoManager.TryIndex<EntityPrototype>(TargetItems[0], out var ep) ? ep.Name : TargetItems[0].ToString();
            msg.AddText(Loc.GetString("achievement-condition-craft-single",
                ("count", RequiredCount), ("target", name)));
        }
        else
        {
            msg.AddText(Loc.GetString("achievement-condition-craft-multi-header",
                ("count", RequiredCount)) + "\n");
            foreach (var item in TargetItems)
            {
                var name = protoManager.TryIndex<EntityPrototype>(item, out var ep) ? ep.Name : item.ToString();
                msg.AddText($"  • {name}\n");
            }
        }

        if (RequiredWorkbenches.Count > 0)
        {
            var benchNames = RequiredWorkbenches
                .Select(id => protoManager.TryIndex<EntityPrototype>(id, out var ep) ? ep.Name : id.ToString())
                .ToList();
            msg.AddText(Loc.GetString("achievement-condition-craft-workbench",
                ("benches", string.Join(", ", benchNames))));
        }

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
        if (!CheckFilters(player, entManager, protoManager))
            return false;

        if (context is not CP14WorkbenchCraftedEvent ev)
            return false;

        if (TargetItems.Count > 0 && !TargetItems.Contains(ev.Result))
            return false;

        if (RequiredWorkbenches.Count > 0)
        {
            var workbenchMeta = entManager.GetComponent<MetaDataComponent>(ev.Workbench);
            if (workbenchMeta.EntityPrototype == null)
                return false;

            if (!RequiredWorkbenches.Any(p => p.Id == workbenchMeta.EntityPrototype.ID))
                return false;
        }

        progress[ProgressKey] = progress.GetValueOrDefault(ProgressKey, 0) + 1;
        return true;
    }
}
