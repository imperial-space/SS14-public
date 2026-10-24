using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>/obj/machinery/quantum_server: грузит виртуальные домены и держит подключения аватаров.</summary>
[RegisterComponent]
public sealed partial class QuantumServerComponent : Component
{
    /// <summary>capacitor_coefficient: множитель остывания.</summary>
    [DataField]
    public float CapacitorCoefficient = 1f;

    /// <summary>scanner_tier: сколько информации о доменах видно в консоли.</summary>
    [DataField]
    public int ScannerTier = 1;

    /// <summary>servo_bonus: прибавка к награде и снижение урона при небезопасном выходе.</summary>
    [DataField]
    public float ServoBonus;

    /// <summary>server_cooldown_time.</summary>
    [DataField]
    public TimeSpan CooldownTime = TimeSpan.FromMinutes(2);

    /// <summary>glitch_chance: шанс глюка, умножается на угрозу.</summary>
    [DataField]
    public float GlitchChance = 0.2f;

    /// <summary>threat_prob_max.</summary>
    [DataField]
    public float ThreatProbMax = 15f;

    /// <summary>multiplayer_bonus.</summary>
    [DataField]
    public float MultiplayerBonus = 1.1f;

    /// <summary>nohit_bonus.</summary>
    [DataField]
    public float NohitBonus = 0.8f;

    /// <summary>points: очки сервера, на них покупаются домены.</summary>
    [DataField]
    public int Points;

    [ViewVariables]
    public bool Emagged;

    [ViewVariables]
    public ProtoId<BitrunningDomainPrototype>? Domain;

    [ViewVariables]
    public EntityUid? DomainMap;

    [ViewVariables]
    public EntityUid? DomainGrid;

    [ViewVariables]
    public bool Randomized;

    /// <summary>domain_complete: ящик доставлен, выходить можно несмотря на глюки.</summary>
    [ViewVariables]
    public bool Complete;

    /// <summary>is_ready: не загружается и не остывает.</summary>
    [ViewVariables]
    public bool Ready = true;

    [ViewVariables]
    public TimeSpan? CoolOffAt;

    /// <summary>Зачистка домена через 15 с после сброса (scrub_vdom).</summary>
    [ViewVariables]
    public TimeSpan? ScrubAt;

    [ViewVariables]
    public TimeSpan StartTime;

    [ViewVariables]
    public int Threat;

    [ViewVariables]
    public int RetriesSpent;

    [ViewVariables]
    public List<EntityCoordinates> ExitSpots = new();

    [ViewVariables]
    public HashSet<(EntityUid Grid, Vector2i Tile)> GoalTiles = new();

    /// <summary>avatar_connection_refs: аватары, подключённые к серверу.</summary>
    [ViewVariables]
    public HashSet<EntityUid> Avatars = new();

    [ViewVariables]
    public List<EntityUid> SpawnedThreats = new();

    [ViewVariables]
    public List<EntityUid> MutationCandidates = new();

    [ViewVariables]
    public List<EntityUid> CustomSpawns = new();

    /// <summary>VIRTUAL_SAFE_AREA: клетки сейфхауса, куда не заходят существа домена.</summary>
    [ViewVariables]
    public HashSet<Vector2i> SafeTiles = new();

    [ViewVariables]
    public int SecondaryLootGenerated;

    /// <summary>Сколько каждой побочной награды ещё можно выдать (secondary_loot убывает).</summary>
    [ViewVariables]
    public Dictionary<EntProtoId, int> SecondaryLootLeft = new();

    [ViewVariables]
    public bool DiskRewardSpawned;

    [ViewVariables]
    public float MainCratePoints;

    [ViewVariables]
    public EntityCoordinates? MainCrateSpot;

    [ViewVariables]
    public TimeSpan NextGoalCheck;

    /// <summary>Уже засчитанные смерти (death_points).</summary>
    [ViewVariables]
    public HashSet<EntityUid> CountedDeaths = new();

    [ViewVariables]
    public TimeSpan NextDeathScan;

    /// <summary>Тайник появляется по очкам (метка loot_signal).</summary>
    [ViewVariables]
    public bool CacheByPoints;

    /// <summary>Тайник выпадает из мегафауны.</summary>
    [ViewVariables]
    public bool CacheByBoss;

    [ViewVariables]
    public bool CacheSpawned;

    /// <summary>Аватар подходил к тайнику.</summary>
    [ViewVariables]
    public bool CacheFound;

    [ViewVariables]
    public int CuriositiesTotal;

    [ViewVariables]
    public int CuriositiesDelivered;

    /// <summary>broadcasting: аватары транслируются на развлекательные экраны.</summary>
    [ViewVariables]
    public bool Broadcasting;

    /// <summary>broadcast_toggle_cd.</summary>
    [ViewVariables]
    public TimeSpan BroadcastCooldownEnd;
}

/// <summary>/obj/machinery/computer/quantum_console.</summary>
[RegisterComponent]
public sealed partial class QuantumConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? Server;
}

/// <summary>/obj/machinery/byteforge: материализует награду рядом с сервером.</summary>
[RegisterComponent]
public sealed partial class ByteforgeComponent : Component
{
    [ViewVariables]
    public List<(EntityUid Cache, TimeSpan At)> Pending = new();
}

/// <summary>/obj/structure/hololadder: выход из домена.</summary>
[RegisterComponent]
public sealed partial class BitrunningHololadderComponent : Component
{
    /// <summary>travel_time.</summary>
    [DataField]
    public TimeSpan TravelTime = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public EntityUid? Server;
}

/// <summary>Метки на карте домена (obj/effect/landmark/bitrunning/*).</summary>
[RegisterComponent]
public sealed partial class BitrunningLandmarkComponent : Component
{
    [DataField(required: true)]
    public BitrunningLandmarkKind Kind;
}

public enum BitrunningLandmarkKind : byte
{
    Hololadder,
    PermanentExit,
    Goal,
    Cache,
    Curiosity,
    LootSignal,
    MobSegment,
    CrateReplacer,
}

/// <summary>Зашифрованный ящик домена: доставить на клетку-цель.</summary>
[RegisterComponent]
public sealed partial class BitrunningEncryptedCacheComponent : Component
{
    /// <summary>true — основной ящик (crate), false — побочный (lockbox curiosity).</summary>
    [DataField]
    public bool Main = true;
}

/// <summary>Расшифрованный ящик-награда: наполняется при создании.</summary>
[RegisterComponent]
public sealed partial class BitrunningDecryptedCacheComponent : Component;

/// <summary>Существо внутри домена (virtual_entity / кандидат в глюки).</summary>
[RegisterComponent]
public sealed partial class BitrunningVirtualEntityComponent : Component
{
    [ViewVariables]
    public EntityUid? Server;

    /// <summary>make_virtual_megafauna: со смертью выпадает зашифрованный ящик.</summary>
    [ViewVariables]
    public bool Megafauna;
}

/// <summary>/datum/component/avatar_connection: аватар и связь с телом в нетподе.</summary>
[RegisterComponent]
public sealed partial class BitrunningAvatarComponent : Component
{
    /// <summary>Зацикленный ambient_buzz домена, слышит только этот битраннер.</summary>
    [ViewVariables]
    public EntityUid? BuzzStream;

    [ViewVariables]
    public TimeSpan NextAmbience;

    [ViewVariables]
    public EntityUid Body;

    [ViewVariables]
    public EntityUid Mind;

    [ViewVariables]
    public EntityUid Server;

    [ViewVariables]
    public EntityUid Netpod;

    /// <summary>nohit: аватар не получал урона.</summary>
    [ViewVariables]
    public bool Nohit = true;

    /// <summary>Подсказка домена для действия «Информация о домене».</summary>
    [ViewVariables]
    public string? HelpText;

    [ViewVariables]
    public bool Disconnecting;
}

/// <summary>Тело битраннера, пока его разум в аватаре.</summary>
[RegisterComponent]
public sealed partial class BitrunningPilotComponent : Component
{
    [ViewVariables]
    public EntityUid Avatar;
}

/// <summary>/obj/item/bitrunning_host_monitor.</summary>
[RegisterComponent]
public sealed partial class BitrunningHostMonitorComponent : Component;

/// <summary>Удочка домена с очками за рыбу (breeze_bay/on_fish_caught).</summary>
[RegisterComponent]
public sealed partial class BitrunningDomainRodComponent : Component
{
    [ViewVariables]
    public EntityUid Server;

    [ViewVariables]
    public int Points;
}

/// <summary>Спавнер гост-роли глюка на месте «мутирующего» существа домена (setup_glitch → spawn_glitch).</summary>
[RegisterComponent]
public sealed partial class BitrunningGlitchSpawnerComponent : Component
{
    [ViewVariables]
    public EntityUid Server;

    [ViewVariables]
    public EntityUid Target;
}

/// <summary>/datum/antagonist/bitrunning_glitch: антагонист домена.</summary>
[RegisterComponent]
public sealed partial class BitrunningGlitchComponent : Component
{
    /// <summary>threat: минимальная угроза сервера; половина вычитается при появлении.</summary>
    [DataField]
    public int Threat;

    /// <summary>Боевое искусство (киберполиция — Спящий карп). ID прототипа Imperial/MartialArts, выдаётся через отражение.</summary>
    [DataField]
    public string? MartialArt;

    /// <summary>preview_outfit: снаряжение, которое возвращается при прорыве на станцию (reset_equipment).</summary>
    [DataField]
    public Robust.Shared.Prototypes.ProtoId<Content.Shared.Roles.StartingGearPrototype>? Gear;

    [ViewVariables]
    public EntityUid? Server;

    /// <summary>Последняя клетка вне сейфхауса — сюда возвращают на границе (virtual_entity).</summary>
    [ViewVariables]
    public Robust.Shared.Map.EntityCoordinates? LastOutside;

    /// <summary>Уже идёт прорыв на станцию.</summary>
    [ViewVariables]
    public bool Breaching;

    /// <summary>Глюк вырвался на станцию (component/glitch).</summary>
    [ViewVariables]
    public bool OnStation;
}

/// <summary>Правило «Глюк битраннинга» (event.dm): мутирует существо в активном домене.</summary>
[RegisterComponent]
public sealed partial class BitrunningGlitchRuleComponent : Component
{
    /// <summary>Роль по выбору админа (прототип спавнера); null — случайная по угрозе.</summary>
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? ForcedSpawner;
}

/// <summary>heretic_hunt: тело, которое можно принести в жертву на руне (очки по рангу).</summary>
[RegisterComponent]
public sealed partial class BitrunningSacrificeComponent : Component
{
    [DataField]
    public float Points = 1;
}

/// <summary>heretic_hunt: большая руна — сюда приносят тела.</summary>
[RegisterComponent]
public sealed partial class BitrunningSacrificeRuneComponent : Component;

/// <summary>simple_bodycam аватара: камера развлекательной сети, пока сервер транслирует.</summary>
[RegisterComponent]
public sealed partial class BitrunningBodycamComponent : Component;
