using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Капельница-пакет (chem_pack): подсоединяется к пациенту и вливает раствор.</summary>
[RegisterComponent]
public sealed partial class ImperialIvBagComponent : Component
{
    [DataField]
    public string Solution = "pack";

    [DataField]
    public FixedPoint2 Rate = 2;

    [DataField]
    public float MaxDistance = 1.5f;

    [ViewVariables]
    public EntityUid? Patient;

    [ViewVariables]
    public TimeSpan NextDrip;

    [DataField]
    public TimeSpan DripDelay = TimeSpan.FromSeconds(1);
}
