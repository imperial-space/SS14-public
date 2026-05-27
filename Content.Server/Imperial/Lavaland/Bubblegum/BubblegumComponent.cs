using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server.Imperial.Lavaland.Bubblegum;

[RegisterComponent]
public sealed partial class BubblegumComponent : Component
{
    // ── Blood blast (after N melee hits) ──────────────────────────────────────

    [DataField] public int MeleeHitsBeforeBlast = 3;
    [DataField] public float BlastRange = 6f;
    [DataField] public float BlastRadius = 1.35f;
    [DataField] public float BlastDamage = 42f;
    [DataField] public float BlastCooldown = 1.8f;
    [DataField] public float TargetSearchRange = 14f;
    [DataField] public SoundSpecifier BlastSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_effects_meteorimpact.ogg");
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextBlastTime;
    [ViewVariables] public int MeleeHits;
    [ViewVariables] public bool BlastReady;

    // ── Blood trail ───────────────────────────────────────────────────────────

    [DataField] public ProtoId<ReagentPrototype> BloodReagent = "Blood";
    [DataField] public float BloodPuddleVolume = 12f;
    [DataField] public float BloodTileDamage = 14f;
    [DataField] public float BloodTileDelay = 0.25f;
    [DataField] public float BloodTrailCooldown = 1.0f;
    [ViewVariables] public TimeSpan NextBloodTrailTime;

    // ── Enrage (< 50% HP, one-time) ───────────────────────────────────────────

    [DataField] public float MaxHp = 2200f;
    [DataField] public float EnrageSpeedMultiplier = 1.45f;
    [DataField] public SoundSpecifier EnrageSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_clockwork_invoke_general.ogg");
    [ViewVariables] public bool IsEnraged;

    // ── Clone assault (< 50% HP) ──────────────────────────────────────────────

    [DataField] public float CloneCooldown = 25f;
    [DataField] public string ClonePrototype = "MobBubblegumClone";
    [DataField] public int CloneCount = 2;
    [DataField] public float CloneSpawnRadius = 7f;
    [ViewVariables] public TimeSpan NextCloneTime;

    // ── Devour ────────────────────────────────────────────────────────────────

    [DataField] public float DevourDamage = 200f;

    // ── Rage (invulnerability) ─────────────────────────────────────────────────

    [DataField] public float RageCooldown = 22f;
    [DataField] public float RageMinDuration = 3.5f;
    [DataField] public float RageMaxDuration = 7f;
    [DataField] public SoundSpecifier RageSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_misc_demon_attack1.ogg");
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextRageTime = TimeSpan.FromSeconds(15);
    [ViewVariables] public bool IsRaging;
    [ViewVariables] public TimeSpan RageEndTime;

    // ── Blood Dive ────────────────────────────────────────────────────────────

    [DataField] public float DiveCooldown = 20f;
    [DataField] public float DiveMaxDistFromPlayer = 4f;
    [DataField] public float DiveMinDistFromPlayer = 0.8f;
    [DataField] public SoundSpecifier DiveSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_misc_enter_blood.ogg");
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextDiveTime = TimeSpan.FromSeconds(20);
    [ViewVariables] public List<EntityUid> BloodTileList = new();

    // ── Blood Hand ────────────────────────────────────────────────────────────

    [DataField] public float HandSpawnInterval = 2f;
    [DataField] public float HandChance = 0.5f;
    [DataField] public string HandPrototype = "MobBubblegumBloodHand";
    [DataField] public float HandDamage = 10f;
    [DataField] public float HandDelay = 0.5f;
    [ViewVariables] public TimeSpan NextHandTime;
    [ViewVariables] public List<(EntityUid HandUid, TimeSpan TriggerTime)> ActiveBloodHands = new();

    // ── Triple Dash ───────────────────────────────────────────────────────────

    [DataField] public string DashMarkerPrototype = "BubblegumDashMarker";
    [DataField] public string DashTrailPrototype = "BubblegumDashTrail";
    [DataField] public float DashSpeed = 18f;
    [DataField] public float DashDamage = 30f;
    [DataField] public float DashMarkerBackOffset = 2f;
    [DataField] public float DashCooldown = 15f;
    [DataField] public List<float> DashLegWaits = new() { 0.9f, 0.6f, 0.3f };
    [DataField] public float DashLegPause = 0.25f;
    [DataField] public float DashMoveDuration = 0.8f;
    [DataField] public float DashTrailInterval = 0.1f;
    [DataField] public SoundSpecifier DashSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_weapons_kenetic_accel.ogg");
    [DataField] public float DashDamageRadius = 1.8f;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextDashTime = TimeSpan.FromSeconds(20);
    [ViewVariables] public bool IsDashWaiting;
    [ViewVariables] public bool IsDashMoving;
    [ViewVariables] public bool IsDashPausing;
    [ViewVariables] public int DashLegIndex;
    [ViewVariables] public TimeSpan DashWaitEndTime;
    [ViewVariables] public TimeSpan DashMoveEndTime;
    [ViewVariables] public TimeSpan DashPauseEndTime;
    [ViewVariables] public EntityCoordinates DashMarkerPos;
    [ViewVariables] public EntityUid DashTargetPlayer;
    [ViewVariables] public TimeSpan LastTrailTime;

    // ── Hallucination Dash (< 50% HP) ─────────────────────────────────────────

    [DataField] public string PhantomPrototype = "BubblegumPhantom";
    [DataField] public float HalluDashCooldown = 25f;
    [DataField] public float CircleHalluCooldown = 30f;
    [DataField] public float RandomHalluCooldown = 22f;
    [DataField] public float PhantomDamage = 15f;
    [DataField] public float PhantomRadius = 2f;
    [DataField] public float PhantomWaitDuration = 0.7f;
    [DataField] public float PhantomDashDuration = 0.45f;
    [DataField] public float PhantomDamageRadius = 1.8f;
    [DataField] public int HalluLegCount = 3;
    [DataField] public float HalluLegPause = 0.35f;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextHalluDashTime = TimeSpan.FromSeconds(30);
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextCircleHalluTime = TimeSpan.FromSeconds(50);
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextRandomHalluTime = TimeSpan.FromSeconds(70);
    [ViewVariables] public bool IsHalluActive;
    [ViewVariables] public bool IsHalluWaiting;
    [ViewVariables] public bool IsHalluMoving;
    [ViewVariables] public bool IsHalluPausing;
    [ViewVariables] public int HalluLegIndex;
    [ViewVariables] public int HalluVariant; // 0=cross/diag, 1=circle, 2=random
    [ViewVariables] public TimeSpan HalluWaitEndTime;
    [ViewVariables] public TimeSpan HalluMoveEndTime;
    [ViewVariables] public TimeSpan HalluPauseEndTime;
    [ViewVariables] public EntityCoordinates HalluMarkerPos;
    [ViewVariables] public bool HalluNeedsNormalDash;
    [ViewVariables] public float HalluCircleAngle;
    // (EntityUid phantom, EntityCoordinates target) — runtime only, not serialised
    public List<(EntityUid Phantom, EntityCoordinates Target)> ActivePhantoms = new();
}
