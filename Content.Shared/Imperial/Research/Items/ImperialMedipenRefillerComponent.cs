using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Машина пополнения медипенов (medipen_refiller).</summary>
[RegisterComponent]
public sealed partial class ImperialMedipenRefillerComponent : Component
{
    [DataField]
    public string Solution = "tank";

    /// <summary>Сколько энергии стоит одна заправка — время в секундах.</summary>
    [DataField]
    public float RefillTime = 2f;
}
