using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Bubblegum;

/// <summary>
/// Пузырь (megafauna/bubblegum из SS13): кровавый след, удары из луж крови, кровавый варп с яростью,
/// тройной рывок, при здоровье ниже половины — рывки с галлюцинациями.
/// </summary>
[RegisterComponent]
public sealed partial class BubblegumComponent : Component
{
    /// <summary>enrage_time.</summary>
    [DataField] public float EnrageTime = 7f;

    /// <summary>move_to_delay 5 -> 3.75 в ярости.</summary>
    [DataField] public float EnrageSpeedMultiplier = 5f / 3.75f;

    [DataField] public float SmackDamage = 10f;
    [DataField] public float ChargeDamage = 30f;
    [DataField] public float HallucinationDamage = 15f;
    [DataField] public int ChargePast = 2;
    [DataField] public float ChargeStepDelay = 0.05f;
    [DataField] public float ChargeCooldown = 1.5f;
    [DataField] public float HallucinationCooldown = 2f;
    [DataField] public int BloodWarpRange = 5;

    [DataField] public SoundSpecifier AttackSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_misc_demon_attack1.ogg");
    [DataField] public SoundSpecifier StepSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_effects_meteorimpact.ogg");
    [DataField] public SoundSpecifier EnterBloodSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_misc_enter_blood.ogg");
    [DataField] public SoundSpecifier ExitBloodSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_exit_blood.ogg");
    [DataField] public SoundSpecifier DeflectSound = new SoundCollectionSpecifier("BulletMiss");

    [DataField] public EntProtoId BloodDecal = "ImperialBubblegumBlood";
    [DataField] public EntProtoId BloodGibs = "ImperialBubblegumBloodGibs";
    [DataField] public EntProtoId Hallucination = "ImperialBubblegumHallucination";
    [DataField] public EntProtoId ChargeMarker = "ImperialBubblegumChargeMarker";
    [DataField] public EntProtoId Decoy = "ImperialBubblegumDecoy";
    [DataField] public EntProtoId DecoyFading = "ImperialBubblegumDecoyFading";
    [DataField] public EntProtoId RightSmack = "ImperialBubblegumRightSmack";
    [DataField] public EntProtoId LeftSmack = "ImperialBubblegumLeftSmack";
    [DataField] public EntProtoId RightGrab = "ImperialBubblegumRightGrab";
    [DataField] public EntProtoId LeftGrab = "ImperialBubblegumLeftGrab";

    /// <summary>Галлюцинация: только рывок, 15 урона, без лута и способностей.</summary>
    [DataField] public bool IsHallucination;

    [ViewVariables] public TimeSpan EnrageTill;
    [ViewVariables] public Vector2i? LastTile;
}

/// <summary>Кровь, по которой Пузырь умеет перемещаться (can_bloodcrawl_in).</summary>
[RegisterComponent]
public sealed partial class BubblegumBloodComponent : Component;
