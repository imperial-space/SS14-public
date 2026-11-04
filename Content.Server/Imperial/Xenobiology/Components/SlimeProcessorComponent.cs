using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>machinery/processor/slime.</summary>
[RegisterComponent]
public sealed partial class SlimeProcessorComponent : Component
{
    /// <summary>rating_amount (бункер первого уровня).</summary>
    [DataField]
    public int RatingAmount = 1;

    /// <summary>rating_speed.</summary>
    [DataField]
    public int RatingSpeed = 1;

    /// <summary>food_processor_process.time = 40 децисекунд.</summary>
    [DataField]
    public float TimePerSlime = 4f;

    [DataField]
    public string Container = "slime_processor";

    [ViewVariables]
    public bool Processing;

    [ViewVariables]
    public TimeSpan ProcessingEnd;

    [ViewVariables]
    public TimeSpan NextPickup;
}
