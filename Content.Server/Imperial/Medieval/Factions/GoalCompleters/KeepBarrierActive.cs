using Content.Server.MagicBarrier;
using Content.Shared.Imperial.Medieval.Factions;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class KeepBarrierActive : FactionGoalCompleter
{
    public override FactionGoalCompleter CreateInstance()
    {
        return new KeepBarrierActive();
    }

    public override float GetCompletion(IEntityManager entMan)
    {
        return MagicBarrierSystem.IsBarrierActive ? 1f : 0f;
    }

    public override string GetDesc(string desctiptionString)
    {
        return desctiptionString;
    }
}
