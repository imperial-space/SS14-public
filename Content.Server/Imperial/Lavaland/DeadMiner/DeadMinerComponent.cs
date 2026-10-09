using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.DeadMiner;

/// <summary>
/// Кровожадный шахтёр (basic/boss/blood_drunk_miner из SS13): серия ударов пилой,
/// очередь из ПКА вблизи, рывок с атакой и очередью издалека, смена формы пилы.
/// </summary>
[RegisterComponent]
public sealed partial class DeadMinerComponent : Component
{
    /// <summary>pka_range: ближе — стреляет, дальше — делает рывок.</summary>
    [DataField] public int PkaRange = 3;

    /// <summary>BB_BDM_RANGED_ATTACK_COOLDOWN.</summary>
    [DataField] public float RangedAttackCooldown = 1.6f;

    // miner_saw: force 8 / open_force 12
    [DataField] public float ClosedDamage = 8f;
    [DataField] public float OpenDamage = 12f;
    [DataField] public int ClosedHits = 5;
    [DataField] public int OpenHits = 3;
    [DataField] public float ClosedHitDelay = 0.3f;
    [DataField] public float OpenHitDelay = 0.5f;
    /// <summary>CLICK_CD_MELEE.</summary>
    [DataField] public float MeleeCooldown = 0.8f;

    // kinetic_accelerator
    [DataField] public EntProtoId KineticProjectile = "ImperialDeadMinerKinetic";
    [DataField] public float PkaCooldown = 1.5f;
    [DataField] public int PkaShots = 3;
    [DataField] public float PkaShotDelay = 0.15f;
    [DataField] public float PkaSpread = 10f;
    [DataField] public float PkaAlertDelay = 0.5f;
    [DataField] public float PkaPrefireDelay = 0.2f;
    [DataField] public float PkaReloadDelay = 0.1f;
    [DataField] public float PkaSpeed = 20f;

    // dash_attack: charge/basic_charge/blood_drunk_miner + rapid_fire
    [DataField] public float DashAttackCooldown = 3f;
    [DataField] public float DashDelay = 0.1f;
    [DataField] public int DashDistance = 6;
    [DataField] public float DashStepDelay = 0.03f;
    [DataField] public float DashFireDelay = 0.22f;

    // transform_weapon: откат 5-10 с
    [DataField] public float TransformCooldownMin = 5f;
    [DataField] public float TransformCooldownMax = 10f;

    [DataField] public SoundSpecifier KineticSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_weapons_kenetic_accel.ogg");
    [DataField] public SoundSpecifier SlashSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");
    [DataField] public SoundSpecifier DashSound = new SoundPathSpecifier("/Audio/Weapons/punchmiss.ogg");
    [DataField] public EntProtoId DeathEffect = "ImperialDeadMinerDeath";

    [ViewVariables] public bool SawOpen;
    [ViewVariables] public bool Busy;
    [ViewVariables] public TimeSpan RangedReady;
    [ViewVariables] public TimeSpan NextPka;
    [ViewVariables] public TimeSpan NextDash;
    [ViewVariables] public TimeSpan NextTransform;
    [ViewVariables] public TimeSpan NextMelee;
}
