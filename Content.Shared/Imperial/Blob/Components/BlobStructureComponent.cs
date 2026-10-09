using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Blob.Components;

[Serializable, NetSerializable]
public enum BlobStructureType : byte
{
    Normal,
    Strong,
    Reflective,
    Core,
    Node,
    Factory,
    Resource,
}

/// <summary>Флаги урона SS13 (damage_flag) — от них зависят броня и реакции штамма.</summary>
public enum BlobDamageFlag : byte
{
    None,
    Melee,
    Bullet,
    Laser,
    Energy,
    Bomb,
    Fire,
    Acid,
}

/// <summary>
/// Структура блоба (obj/structure/blob). Здоровье — atom_integrity, считается вручную,
/// чтобы повторить brute_resist/fire_resist, броню по флагам и реакции штамма.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlobStructureComponent : Component
{
    [DataField("structureType")]
    public BlobStructureType Type = BlobStructureType.Normal;

    [DataField]
    public float MaxIntegrity = 25f;

    /// <summary>initial_integrity (обычный блоб появляется с 21 из 25).</summary>
    [DataField]
    public float? InitialIntegrity;

    /// <summary>health_regen при пульсе.</summary>
    [DataField]
    public float HealthRegen = 1f;

    [DataField]
    public float BruteResist = 0.5f;

    [DataField]
    public float FireResist = 1f;

    /// <summary>point_return; меньше 0 — нельзя удалить.</summary>
    [DataField]
    public int PointReturn;

    /// <summary>armor_type: процент защиты по флагу.</summary>
    [DataField]
    public Dictionary<BlobDamageFlag, float> Armor = new()
    {
        { BlobDamageFlag.Fire, 80 },
        { BlobDamageFlag.Acid, 70 },
        { BlobDamageFlag.Laser, 50 },
    };

    /// <summary>atmosblock (сильный блоб держит атмос выше 50% здоровья).</summary>
    [DataField]
    public bool AtmosBlock;

    /// <summary>ignore_syncmesh_share.</summary>
    [DataField]
    public bool IgnoreSyncMesh;

    /// <summary>Бьёт ли ядро/узел область вокруг (claim/pulse/expand).</summary>
    [DataField] public int ClaimRange;
    [DataField] public int PulseRange;
    [DataField] public int ExpandRange;
    [DataField] public int StrongReinforceRange;
    [DataField] public int ReflectorReinforceRange;

    /// <summary>Период process() (SSobj — 2 с).</summary>
    [DataField]
    public TimeSpan ProcessInterval = TimeSpan.FromSeconds(2);

    /// <summary>BLOB_FACTORY_MAX_SPORES.</summary>
    [DataField]
    public int MaxSpores = 3;

    /// <summary>BLOBMOB_SPORE_SPAWN_COOLDOWN.</summary>
    [DataField]
    public TimeSpan SporeCooldown = TimeSpan.FromSeconds(8);

    /// <summary>BLOB_RESOURCE_GATHER_DELAY / ADDED_DELAY / AMOUNT.</summary>
    [DataField] public TimeSpan GatherDelay = TimeSpan.FromSeconds(4);
    [DataField] public TimeSpan GatherAddedDelay = TimeSpan.FromSeconds(0.25);
    [DataField] public float GatherAmount = 1f;

    /// <summary>
    /// Поправка под оружие SS14 (оно слабее SS13): пули 16-19 против 25-30, лазер 14 против 20,
    /// сварка 8 против 15. Брут ближнего боя в SS14 не слабее — без поправки.
    /// </summary>
    [DataField] public float RangedBruteMultiplier = 1.6f;
    [DataField] public float RangedBurnMultiplier = 1.45f;
    [DataField] public float MeleeBurnMultiplier = 1.9f;
    [DataField] public float MeleeBruteMultiplier = 1f;

    [DataField]
    public string BaseState = "blob";

    [DataField] public SoundSpecifier BruteHitSound = new SoundPathSpecifier("/Audio/Imperial/blob/attackblob.ogg");
    [DataField] public SoundSpecifier TapSound = new SoundPathSpecifier("/Audio/Imperial/blob/tap.ogg");
    [DataField] public SoundSpecifier BurnHitSound = new SoundPathSpecifier("/Audio/Imperial/blob/welder.ogg");
    [DataField] public SoundSpecifier DestroySound = new SoundPathSpecifier("/Audio/Imperial/blob/splat.ogg");

    /// <summary>Оверманд-владелец; null — мёртвый блоб.</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Overmind;

    [ViewVariables(VVAccess.ReadWrite)]
    public float Integrity = -1f;

    [ViewVariables] public TimeSpan PulseCooldown;
    [ViewVariables] public TimeSpan HealCooldown;
    [ViewVariables] public TimeSpan NextProcess;

    /// <summary>Тайл засчитывается к победе (на станции).</summary>
    [ViewVariables] public bool Legit;

    // Фабрика
    [ViewVariables] public HashSet<EntityUid> Spores = new();
    [ViewVariables] public TimeSpan NextSpore;
    [ViewVariables] public EntityUid? Blobbernaut;
    [ViewVariables] public bool CreatingBlobbernaut;

    // Ресурсный блоб
    [ViewVariables] public TimeSpan NextGather;

    /// <summary>Тик последнего взрыва (флаг BOMB).</summary>
    [ViewVariables] public GameTick LastExplosionTick;

    /// <summary>Тик последнего удара в ближнем бою (флаг MELEE, а не LASER, для ожогов).</summary>
    [ViewVariables] public GameTick LastMeleeTick;

    /// <summary>Не бить урон повторно при внутреннем лечении.</summary>
    [ViewVariables] public bool IgnoreDamage;
}
