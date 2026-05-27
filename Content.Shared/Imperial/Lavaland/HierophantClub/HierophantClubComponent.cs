using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Lavaland.HierophantClub;

/// <summary>
/// Посох Иерофанта. Три атаки в зависимости от типа клика:
/// • Клик по цели (Entity) → атака «Гончие» (chasers)
/// • Клик по ближнему тайлу (≤ NearbyTileRange) → «Заряд по площади» (square ring)
/// • Клик по дальнему тайлу → «Удар крестом» (cardinal lines)
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HierophantClubComponent : Component
{
    // ── Charges ───────────────────────────────────────────────────────────────

    /// <summary>Текущее количество зарядов.</summary>
    [DataField, AutoNetworkedField]
    public int Charges = 8;

    [DataField]
    public int MaxCharges = 8;

    /// <summary>Время восстановления одного заряда (секунды).</summary>
    [DataField]
    public float ChargeRegenTime = 30f;

    [ViewVariables]
    public TimeSpan NextChargeTime;

    // ── Attack: Chasers (Гончие) ──────────────────────────────────────────────

    [DataField]
    public float ChaserStepDelay = 0.22f;

    [DataField]
    public float ChaserDuration = 9f;

    [DataField]
    public float ChaserDamage = 10f;

    [DataField]
    public int MaxChasers = 2;

    // ── Attack: Cross / Line (Удар крестом) ───────────────────────────────────

    [DataField]
    public float CrossBlastDamage = 20f;

    [DataField]
    public int LineAttackLength = 6;

    // ── Attack: Square Ring (Заряд по площади) ────────────────────────────────

    [DataField]
    public float SquareRingBaseDamage = 10f;

    [DataField]
    public float SquareRingDamageStep = 8f;

    [DataField]
    public int SquareRingCount = 3;

    [DataField]
    public float SquareRingStepDelay = 0.25f;

    // ── Tile click distance threshold ─────────────────────────────────────────

    /// <summary>
    /// Клик ≤ этого расстояния (в тайлах) от игрока → ring-атака.
    /// Клик дальше → cross-атака.
    /// </summary>
    [DataField]
    public float NearbyTileRange = 2.5f;

    // ── Tile damage delay ─────────────────────────────────────────────────────

    [DataField]
    public float TileDamageDelay = 0.5f;

    // ── Effects ───────────────────────────────────────────────────────────────

    [DataField]
    public EntProtoId BlastEffectPrototype = "ImperialHierophantBlast";

    [DataField]
    public EntProtoId SquareEffectPrototype = "ImperialHierophantSquare";

    // ── Sounds ────────────────────────────────────────────────────────────────

    [DataField]
    public SoundSpecifier LineAttackSound =
        new SoundPathSpecifier("/Audio/Imperial/boss/sound_weapons_sonic_jackhammer.ogg");

    [DataField]
    public SoundSpecifier ChaserAttackSound =
        new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_blind.ogg");

    [DataField]
    public SoundSpecifier RingAttackSound =
        new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_repulse.ogg");

    [DataField]
    public SoundSpecifier NoChargesSound =
        new SoundPathSpecifier("/Audio/Weapons/Guns/EmptyAlarm/empty_alarm.ogg");
}
