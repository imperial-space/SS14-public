using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>Принц: таран princely_charge.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderPrinceComponent : Component
{
    [ViewVariables]
    public bool Charging;

    [DataField]
    public TimeSpan PrinceImpactTime = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan PrinceImpactBoozePowerDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public float PrinceChargeBaseThrowSpeed = 12f;

    [DataField]
    public float PrinceImpactAmount = 60f;

    [DataField]
    public float PrinceImpactAmount2 = 1000f;

    [DataField]
    public float PrinceImpactAmount3 = 150f;

    [DataField]
    public float PrinceImpactBaseThrowSpeed = 12f;
}
