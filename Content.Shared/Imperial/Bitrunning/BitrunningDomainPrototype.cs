using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>
/// /datum/lazy_template/virtual_domain: виртуальный домен. Раскладка карты лежит в отдельном файле
/// (конвертирован из _maps/virtual_domains/*.dmm), сервер строит её кодом на своей карте.
/// </summary>
[Prototype]
public sealed partial class BitrunningDomainPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId Description = "bitrunning-domain-no-desc";

    /// <summary>help_text: подсказка в действии «Информация о домене».</summary>
    [DataField]
    public LocId? Help;

    /// <summary>Что делать на локации: текст цели «добыть тайник».</summary>
    [DataField]
    public LocId? Objective;

    [DataField]
    public int Cost;

    [DataField]
    public BitrunningDifficulty Difficulty;

    /// <summary>reward_points.</summary>
    [DataField]
    public int Reward = 1;

    [DataField]
    public BitrunningDomainFlags Flags;

    /// <summary>Раскладка домена (Resources/...).</summary>
    [DataField(required: true)]
    public ResPath Layout;

    /// <summary>forced_outfit: снаряжение аватара вместо выбранного в нетподе.</summary>
    [DataField]
    public ProtoId<Content.Shared.Roles.StartingGearPrototype>? ForcedOutfit;

    /// <summary>completion_loot: что положить в ящик награды (прототип → количество).</summary>
    [DataField]
    public Dictionary<EntProtoId, int> CompletionLoot = new();

    /// <summary>secondary_loot: награда за побочные цели (вес/количество).</summary>
    [DataField]
    public Dictionary<EntProtoId, int> SecondaryLoot = new();

    /// <summary>mob_modules: группы мобов для меток модульных сегментов.</summary>
    [DataField]
    public List<ProtoId<BitrunningMobSegmentPrototype>> MobModules = new();

    /// <summary>modular_unique_mobs.</summary>
    [DataField]
    public bool UniqueMobModules;

    /// <summary>is_modular: в интерфейсе отмечается значком.</summary>
    [DataField]
    public bool Modular;

    /// <summary>main_crate_point_goal: домены, где ящик появляется после набора очков.</summary>
    [DataField]
    public int CratePointGoal = 10;

    /// <summary>Освещение карты домена (fullbright-зоны TG). null — темно.</summary>
    [DataField]
    public Color? AmbientLight;

    /// <summary>ambientsounds области: случайный звук из набора раз в 30–60 секунд.</summary>
    [DataField]
    public ProtoId<Robust.Shared.Audio.SoundCollectionPrototype>? Ambience;

    /// <summary>ambient_buzz области: зацикленный фон, пока аватар в домене.</summary>
    [DataField]
    public Robust.Shared.Audio.SoundSpecifier? AmbientBuzz;

    /// <summary>Свет под крышей (тёмные области TG): почти чёрный, чтобы силуэты угадывались без ламп.</summary>
    [DataField]
    public Color RoofLight = Color.FromHex("#0b0b10");

    /// <summary>grasslands_hunt / island_brawl: очки к ящику за каждую смерть в домене (фильтр — только эти прототипы).</summary>
    [DataField]
    public float DeathPoints;

    [DataField]
    public List<EntProtoId> DeathFilter = new();

    /// <summary>beach_bar: очки за каждый глоток напитка аватаром.</summary>
    [DataField]
    public float DrinkPoints;

    /// <summary>custom_spawns: аватары появляются на месте этих сущностей (island_brawl — спавнеры островитян).</summary>
    [DataField]
    public EntProtoId? CustomSpawn;

    /// <summary>breeze_bay: очки к ящику за каждую пойманную удочкой домена рыбу.</summary>
    [DataField]
    public int FishingPoints;

    /// <summary>keep_custom_spawns.</summary>
    [DataField]
    public bool KeepCustomSpawns;

    /// <summary>announce_to_ghosts: в домене есть роли призраков.</summary>
    [DataField]
    public bool AnnounceGhosts;
}
