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

    [DataField]
    public float GetAvatarDestinationRange = 0.3f;

    [DataField]
    public float ScanSacrificesRange = 1.5f;
}
