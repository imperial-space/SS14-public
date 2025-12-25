using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.SmithingSystem.Bui;

[Serializable] [NetSerializable]
public sealed class SmithGameData : BoundUserInterfaceState
{
    public EntProtoId ItemProtoId;
    public float SpawnTime;

    public Stack<SmithStepData> Steps = new();

    public float CalculateTotalTime()
    {
        var stepsCount = Steps.Count;

        var totalTime = SpawnTime * stepsCount;

        foreach (var step in Steps)
        {
            totalTime += step.PerfectHitTime + step.GoodHitTime * 2;
        }

        return totalTime;
    }
}
