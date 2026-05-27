using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.Chaos;

[RegisterComponent]
public sealed partial class BloodContractComponent : Component
{
    [DataField]
    public EntProtoId BerserkerWeaponPrototype = "Chainsaw";

    [DataField]
    public float BerserkerStimulantsAmount = 10f;

    [DataField]
    public float BerserkerRageDurationSeconds = 300f;

    [DataField]
    public string BerserkerObjective = "ChaosRipAndTearTargetObjective";

    [DataField]
    public EntProtoId HunterWeaponPrototype = "ButchCleaver";

    [DataField]
    public float HunterRageDurationSeconds = 120f;

    [DataField]
    public string HunterObjective = "ChaosRipAndTearTargetObjective";
}
