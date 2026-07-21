using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

public abstract partial class BaseStructureCondition : AchievementCondition
{
    [DataField(required: true)]
    public List<string> TargetPrototypes = new();

    public override FormattedMessage GetDescription(IPrototypeManager proto)
    {
        var msg = new FormattedMessage();
        msg.PushColor(Color.FromHex("#9e8c78"));

        var names = TargetPrototypes
            .Select(id => proto.TryIndex<EntityPrototype>(id, out var ep) ? ep.Name : id)
            .Distinct()
            .ToList();

        var targets = string.Join(", ", names);
        
        msg.AddText(Loc.GetString("achievement-condition-break-structure", 
            ("count", RequiredCount), ("targets", targets)));

        msg.Pop();
        AppendRequirements(msg, proto);
        return msg;
    }

    public override bool Check(EntityUid player, IEntityManager entManager, IPrototypeManager protoManager, object? context, Dictionary<string, int> progress)
    {
        return CheckFilters(player, entManager, protoManager) && progress.GetValueOrDefault(ProgressKey, 0) >= RequiredCount;
    }

    public override bool TryUpdateProgress(EntityUid player, IEntityManager entManager, IPrototypeManager protoManager, object? context, Dictionary<string, int> progress)
    {
        if (!CheckFilters(player, entManager, protoManager) || context is not EntityUid brokenEnt)
            return false;

        var meta = entManager.GetComponent<MetaDataComponent>(brokenEnt);
        
        if (meta.EntityPrototype == null || !TargetPrototypes.Contains(meta.EntityPrototype.ID))
            return false;

        progress[ProgressKey] = progress.GetValueOrDefault(ProgressKey, 0) + 1;
        return true;
    }
}
