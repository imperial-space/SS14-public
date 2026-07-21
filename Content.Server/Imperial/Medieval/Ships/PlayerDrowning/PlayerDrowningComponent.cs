using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Ships.PlayerDrowning;

[RegisterComponent]
public sealed partial class PlayerDrowningComponent : Component
{
    [DataField("drownTime")]
    public float DrownTime;

    [DataField("maxDrownTime")]
    public float MaxDrownTime = 25;

    [DataField("damageDrownDelay")]
    public float DamageDrownDelay = 7;

    [DataField("speedModifier")]
    public float SpeedModifier = 0.5f;

    [DataField("drowningDamage")]
    public DamageSpecifier DrowningDamage = new()
    {
        DamageDict = new()
        {
            { "Asphyxiation", 10 }
        }
    };

    [DataField]
    public EntProtoId SplashEffect = "MedievalShipSplashEffect";
}
