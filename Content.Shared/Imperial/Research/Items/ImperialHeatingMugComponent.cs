using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Кружка Мауна (maunamug): подогревает содержимое, пока включена.</summary>
[RegisterComponent]
public sealed partial class ImperialHeatingMugComponent : Component
{
    [DataField]
    public string Solution = "drink";

    [DataField]
    public float TargetTemperature = 340f;

    [DataField]
    public float HeatPerSecond = 15f;

    [DataField]
    public bool Enabled;
}
