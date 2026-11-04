using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Генные ножницы (geneshears): вырезают случайное свойство из растения в лотке.</summary>
[RegisterComponent]
public sealed partial class ImperialGeneShearsComponent : Component
{
    [DataField]
    public float Delay = 5f;
}
