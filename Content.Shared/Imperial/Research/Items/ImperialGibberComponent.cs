using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Мясорубка (gibber): перемалывает мёртвое или умирающее тело в мясо.</summary>
[RegisterComponent]
public sealed partial class ImperialGibberComponent : Component
{
    [DataField]
    public float GibTime = 10f;

    [DataField]
    public EntProtoId DefaultMeat = "FoodMeat";
}
