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
    [DataField] public TimeSpan RangedAttackCooldown = TimeSpan.FromSeconds(1.6f);

    // miner_saw: force 8 / open_force 12
    [DataField] public float ClosedDamage = 8f;
    [DataField] public float OpenDamage = 12f;
    [DataField] public int ClosedHits = 5;
    [DataField] public int OpenHits = 3;
    [DataField] public TimeSpan ClosedHitDelay = TimeSpan.FromSeconds(0.3f);
    [DataField] public TimeSpan OpenHitDelay = TimeSpan.FromSeconds(0.5f);
    /// <summary>CLICK_CD_MELEE.</summary>
    [DataField] public TimeSpan MeleeCooldown = TimeSpan.FromSeconds(0.8f);

    // kinetic_accelerator
    [DataField] public EntProtoId KineticProjectile = "ImperialDeadMinerKinetic";
    [DataField] public TimeSpan PkaCooldown = TimeSpan.FromSeconds(1.5f);
    [DataField] public int PkaShots = 3;
    [DataField] public TimeSpan PkaShotDelay = TimeSpan.FromSeconds(0.15f);
    [DataField] public float PkaSpread = 10f;
    [DataField] public TimeSpan PkaAlertDelay = TimeSpan.FromSeconds(0.5f);
    [DataField] public TimeSpan PkaPrefireDelay = TimeSpan.FromSeconds(0.2f);
    [DataField] public TimeSpan PkaReloadDelay = TimeSpan.FromSeconds(0.1f);
    [DataField] public float PkaSpeed = 20f;

    // dash_attack: charge/basic_charge/blood_drunk_miner + rapid_fire
    [DataField] public TimeSpan DashAttackCooldown = TimeSpan.FromSeconds(3f);
    [DataField] public TimeSpan DashDelay = TimeSpan.FromSeconds(0.1f);
    [DataField] public int DashDistance = 6;
    [DataField] public TimeSpan DashStepDelay = TimeSpan.FromSeconds(0.03f);
    [DataField] public TimeSpan DashFireDelay = TimeSpan.FromSeconds(0.22f);

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
