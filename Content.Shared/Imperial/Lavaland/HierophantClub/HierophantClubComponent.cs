using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Lavaland.HierophantClub;

[RegisterComponent, NetworkedComponent]
public sealed partial class HierophantClubComponent : Component
{
    [DataField] public int Charges = 3;
    [DataField] public int MaxCharges = 3;
    [DataField] public float ChargeRegenTime = 8f;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextChargeTime;

    [DataField] public float NearbyTileRange = 4f;
    [DataField] public float TileDamageDelay = 0.4f;

    // Line / cross attack
    [DataField] public int LineAttackLength = 5;
    [DataField] public float CrossBlastDamage = 30f;
    [DataField] public EntProtoId BlastEffectPrototype = "ImperialHierophantBlast";
    [DataField] public SoundSpecifier LineAttackSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/laser_cannon2.ogg");
    [DataField] public SoundSpecifier NoChargesSound = new SoundPathSpecifier("/Audio/Effects/pop.ogg");

    // Square ring attack
    [DataField] public int SquareRingCount = 3;
    [DataField] public float SquareRingBaseDamage = 20f;
    [DataField] public float SquareRingDamageStep = 5f;
    [DataField] public float SquareRingStepDelay = 0.3f;
    [DataField] public EntProtoId SquareEffectPrototype = "ImperialHierophantSquare";
    [DataField] public SoundSpecifier RingAttackSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/laser_cannon.ogg");

    // Chaser attack
    [DataField] public int MaxChasers = 3;
    [DataField] public float ChaserDamage = 25f;
    [DataField] public float ChaserStepDelay = 0.15f;
    [DataField] public float ChaserDuration = 3f;
    [DataField] public SoundSpecifier ChaserAttackSound = new SoundPathSpecifier("/Audio/Imperial/EnergyCore/core_emitter_laser.ogg");
}
