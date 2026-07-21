using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class BecomeEntityCondition : AchievementCondition
{
    [DataField(required: true)]
    public List<EntProtoId> TargetPrototypes = new();

    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var msg = new FormattedMessage();
        msg.PushColor(Color.FromHex("#9e8c78"));

        var names = TargetPrototypes
            .Select(id => protoManager.TryIndex<EntityPrototype>(id, out var ep) ? ep.Name : id.ToString())
            .Distinct()
            .ToList();

        var targets = string.Join(", ", names);
        
        msg.AddText(Loc.GetString("achievement-condition-become-entity", 
            ("count", RequiredCount), ("targets", targets)));

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
        if (!CheckFilters(player, entManager, protoManager) || context is not EntityUid attachedEnt)
            return false;

        var meta = entManager.GetComponent<MetaDataComponent>(attachedEnt);
        if (meta.EntityPrototype == null || !TargetPrototypes.Contains(meta.EntityPrototype.ID))
            return false;

        progress[ProgressKey] = progress.GetValueOrDefault(ProgressKey, 0) + 1;
        return true;
    }
}