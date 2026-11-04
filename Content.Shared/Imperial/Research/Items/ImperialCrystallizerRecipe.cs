using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

[DataDefinition]
public sealed partial class ImperialCrystallizerRecipe
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public EntProtoId Result;

    /// <summary>Индекс газа -> моли на один кристалл.</summary>
    [DataField(required: true)]
    public Dictionary<int, float> Gases = new();

    /// <summary>Секунды работы на один кристалл.</summary>
    [DataField]
    public float Time = 30f;
}
