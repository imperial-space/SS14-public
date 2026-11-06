using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Кровавый охладитель: перезаряжается кровью владельца.</summary>
[RegisterComponent]
public sealed partial class SlimeBloodChillerComponent : Component
{
    [ViewVariables]
    public TimeSpan NextCharge;

    [DataField]
    public TimeSpan ChargeDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public FixedPoint2 UpdateItemsAmount = -20;

    [DataField]
    public float UpdateItemsAmount2 = 5f;
}
