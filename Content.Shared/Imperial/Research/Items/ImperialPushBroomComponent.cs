using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Метла-толкач (pushbroom): в двух руках сметает предметы перед собой.</summary>
[RegisterComponent]
public sealed partial class ImperialPushBroomComponent : Component
{
    [DataField]
    public int MaxItems = 8;
}
