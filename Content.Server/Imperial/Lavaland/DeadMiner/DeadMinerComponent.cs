using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.DeadMiner;

[RegisterComponent]
public sealed partial class DeadMinerComponent : Component
{
    [DataField] public float MaxHp = 900f;
    [DataField] public float TargetSearchRange = 20f;

    // ── Mode switch ───────────────────────────────────────────────────────────
    // false = miner (сложенное оружие, ближний бой)
    // true  = miner_transformed (разложенное, активный режим)

    /// <summary>Дистанция в тайлах. Ниже — режим 1, выше — режим 2.</summary>
    [DataField] public float ModeTransformRange = 1.5f;

    [ViewVariables] public bool IsTransformed;

    // ── Melee ─────────────────────────────────────────────────────────────────

    /// <summary>Урон в режиме 1 (miner). КД ≈ 0.3 с → attackRate = 1/0.3.</summary>
    [DataField] public float MeleeMode1Damage = 6f;

    [DataField] public float MeleeMode1AttackRate = 1f / 0.3f;   // ~3.33/s

    /// <summary>Урон в режиме 2 (miner_transformed). КД ≈ 0.5 с.</summary>
    [DataField] public float MeleeMode2Damage = 10f;

    [DataField] public float MeleeMode2AttackRate = 2f;          // 2/s

    // ── Kinetic shot ─────────────────────────────────────────────────────────

    /// <summary>Минимальная дистанция до цели для выстрела.</summary>
    [DataField] public float KineticMinRange = 1f;

    /// <summary>Максимальная дистанция до цели для выстрела.</summary>
    [DataField] public float KineticMaxRange = 4f;

    [DataField] public float KineticCooldown = 1.5f;

    [DataField] public float KineticProjectileSpeed = 15f;

    [DataField] public string KineticBulletPrototype = "BulletDeadMinerKinetic";

    [ViewVariables] public TimeSpan NextKineticTime;

    // ── Jump (телепорт к игроку) ───────────────────────────────────────────────

    /// <summary>Прыжок активируется если дистанция &gt; этого значения.</summary>
    [DataField] public float JumpTriggerRange = 4f;

    /// <summary>На сколько тайлов от игрока приземляется шахтер.</summary>
    [DataField] public float JumpLandDistFromPlayer = 2f;

    [DataField] public float JumpCooldown = 8f;

    [DataField] public string SmokePrototype = "EffectDeadMinerSmoke";

    [ViewVariables] public TimeSpan NextJumpTime;

    // ── Sounds ────────────────────────────────────────────────────────────────

    [DataField] public SoundSpecifier AttackSound =
        new SoundPathSpecifier("/Audio/Imperial/boss/sound_weapons_kenetic_accel.ogg");

    [DataField] public SoundSpecifier JumpSound =
        new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_wand_teleport.ogg");

    [DataField] public SoundSpecifier TransformSound =
        new SoundPathSpecifier("/Audio/Imperial/boss/sound_effects_bin_close.ogg");

}
