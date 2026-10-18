using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Hierophant;

/// <summary>
/// Иерофант (megafauna/hierophant из SS13): преследователи, кресты взрывов, телепорт к цели,
/// расходящаяся вспышка, арена из стен и возврат к маяку.
/// </summary>
[RegisterComponent]
public sealed partial class HierophantComponent : Component
{
    [DataField] public int BaseBurstRange = 3;
    [DataField] public int BaseBeamRange = 5;
    [DataField] public float RangedCooldownTime = 4f;
    [DataField] public float MajorAttackCooldown = 6f;
    [DataField] public float ChaserCooldownTime = 10.1f;
    [DataField] public float ArenaCooldownTime = 20f;
    [DataField] public float BlastDamage = 10f;
    [DataField] public float BlinkBlastDamage = 30f;

    [DataField] public SoundSpecifier BlastSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_blind.ogg");
    [DataField] public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_weapons_sear.ogg");
    [DataField] public SoundSpecifier TelegraphSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_effects_bin_close.ogg");
    [DataField] public SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_wand_teleport.ogg");
    [DataField] public SoundSpecifier BurstSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_machines_airlock_open.ogg");
    [DataField] public SoundSpecifier MoveSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_mecha_mechmove04.ogg");

    [DataField] public EntProtoId Blast = "ImperialHierophantBlastDamaging";
    [DataField] public EntProtoId Squares = "ImperialHierophantSquares";
    [DataField] public EntProtoId Wall = "ImperialHierophantWall";
    [DataField] public EntProtoId Telegraph = "ImperialHierophantTelegraph";
    [DataField] public EntProtoId TelegraphCardinal = "ImperialHierophantTelegraphCardinal";
    [DataField] public EntProtoId TelegraphDiagonal = "ImperialHierophantTelegraphDiagonal";
    [DataField] public EntProtoId TelegraphTeleport = "ImperialHierophantTelegraphTeleport";
    [DataField] public EntProtoId Beacon = "ImperialHierophantBeacon";

    [ViewVariables] public int BurstRange = 3;
    [ViewVariables] public int BeamRange = 5;
    [ViewVariables] public float Anger;
    [ViewVariables] public float ChaserSpeed = 3f;
    [ViewVariables] public bool Blinking;
    [ViewVariables] public bool SittingAtCenter = true;
    [ViewVariables] public TimeSpan RangedCooldown;
    [ViewVariables] public TimeSpan ChaserCooldown;
    [ViewVariables] public TimeSpan ArenaCooldown;
    [ViewVariables] public TimeSpan? GoHomeAt;
    [ViewVariables] public EntityUid? SpawnedBeacon;
    [ViewVariables] public Vector2i? LastTile;
}

/// <summary>Временная стена арены: пропускает только своего иерофанта.</summary>
[RegisterComponent]
public sealed partial class HierophantWallComponent : Component
{
    [ViewVariables] public EntityUid? Caster;
}
