using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.StationGoal;


[Prototype]
public sealed partial class StationGoalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Text { get; set; } = string.Empty;
}
