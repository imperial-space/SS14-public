using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class DestroyFactionObeliskCondition : AchievementCondition
{
    [DataField(required: true)]
    public ProtoId<MedievalFactionPrototype> TargetFaction = default!;

    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var msg = new FormattedMessage();
        msg.PushColor(Color.FromHex("#9e8c78"));
        
        var factionName = TargetFaction.Id;
        if (protoManager.TryIndex(TargetFaction, out var factionProto))
            factionName = Loc.GetString(factionProto.Name);

        msg.AddText(Loc.GetString("achievement-condition-destroy-specific-crystal", 
            ("count", RequiredCount), ("faction", factionName)));

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

        if (context is not DestroyFactionObeliskContext destroyObeliskContext ||
            destroyObeliskContext.FactionId != TargetFaction)
            return false;

        progress[ProgressKey] = progress.GetValueOrDefault(ProgressKey, 0) + 1;
        return true;
    }
}

public record DestroyFactionObeliskContext(ProtoId<MedievalFactionPrototype> FactionId);