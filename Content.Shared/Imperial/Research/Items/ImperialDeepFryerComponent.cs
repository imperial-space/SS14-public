using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Фритюрница (deep_fryer): обжаривает вставленный предмет.</summary>
[RegisterComponent]
public sealed partial class ImperialDeepFryerComponent : Component
{
    [DataField]
    public string Slot = "fryer";

    [DataField]
    public float FryTime = 10f;
}
