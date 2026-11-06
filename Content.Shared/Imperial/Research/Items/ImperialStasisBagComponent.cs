using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Стазисный мешок для тел: замедляет метаболизм содержимого.</summary>
[RegisterComponent]
public sealed partial class ImperialStasisBagComponent : Component
{
    [DataField]
    public float Multiplier = 10f;
}
