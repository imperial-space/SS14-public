using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

[Serializable, NetSerializable]
public sealed class FactionGoalData
{
    public ProtoId<FactionGoalPrototype> GoalProto;

    public string Desctiption = string.Empty;

    public float Progress = 0f;

    [NonSerialized]
    public FactionGoalCompleter Completer = default!;

    public FactionGoalData(FactionGoalPrototype goalProto, IEntityManager entMan)
    {
        GoalProto = goalProto.ID;
        Completer = goalProto.Completer.CreateInstance();
        Desctiption = Completer.GetDesc(goalProto.Description);
        Progress = Completer.GetCompletion(entMan);
    }
}
