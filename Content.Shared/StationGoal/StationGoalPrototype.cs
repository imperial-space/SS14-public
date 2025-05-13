using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.StationGoal;


[Prototype, Serializable]
public sealed class StationGoalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField]
    public string Text { get; set; } = string.Empty;
}
