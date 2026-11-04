using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Дым-машина (smoke_machine): выпускает облака дыма из своего раствора.</summary>
[RegisterComponent]
public sealed partial class ImperialSmokeMachineComponent : Component
{
    [DataField]
    public string Solution = "tank";

    [DataField]
    public bool Enabled;

    [DataField]
    public FixedPoint2 PerPuff = 10;

    [DataField]
    public int Spread = 8;

    [DataField]
    public float Interval = 5f;

    [ViewVariables]
    public TimeSpan NextPuff;
}
