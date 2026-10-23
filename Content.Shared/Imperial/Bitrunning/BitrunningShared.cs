using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>Сложность домена (BITRUNNER_DIFFICULTY_*).</summary>
[Serializable, NetSerializable]
public enum BitrunningDifficulty : byte
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}

/// <summary>DOMAIN_* флаги.</summary>
[Flags, Serializable, NetSerializable]
public enum BitrunningDomainFlags : byte
{
    None = 0,
    ForbidsItems = 1 << 0,
    ForbidsAbilities = 1 << 1,
    NoNohitBonus = 1 << 2,
    TestOnly = 1 << 3,
}

public static class BitrunningConstants
{
    public const int CostNone = 0;
    public const int CostLow = 1;
    public const int CostMedium = 2;
    public const int CostHigh = 3;
    public const int CostExtreme = 20;

    /// <summary>Нетподы, байтфоржи и ящики ищут сервер в этом радиусе (oview(4)).</summary>
    public const float LinkRange = 4.5f;
}

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

/// <summary>/datum/modular_mob_segment: набор мобов, из которого выбираются существа для метки.</summary>
[Prototype]
public sealed partial class BitrunningMobSegmentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<string> Mobs = new();

    /// <summary>exact: всех мобов из списка; иначе случайные.</summary>
    [DataField]
    public bool Exact;

    /// <summary>max: сколько мобов выбрать, если не exact.</summary>
    [DataField]
    public int Max = 4;

    /// <summary>probability: шанс появления каждого моба.</summary>
    [DataField]
    public float Probability = 1f;
}

#region Консоль

[Serializable, NetSerializable]
public enum QuantumConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class QuantumConsoleDomainEntry(string id, string name, string description, int cost, BitrunningDifficulty difficulty, string reward, bool modular, bool secondary, bool ghosts = false)
{
    public bool Ghosts = ghosts;
    public string Id = id;
    public string Name = name;
    public string Description = description;
    public int Cost = cost;
    public BitrunningDifficulty Difficulty = difficulty;
    public string Reward = reward;
    public bool Modular = modular;
    public bool Secondary = secondary;
}

[Serializable, NetSerializable]
public sealed class QuantumConsoleAvatarEntry(string name, string pilot, float health, float brute, float burn, float toxin, float airloss)
{
    public string Name = name;
    public string Pilot = pilot;
    public float Health = health;
    public float Brute = brute;
    public float Burn = burn;
    public float Toxin = toxin;
    public float Airloss = airloss;
}

[Serializable, NetSerializable]
public sealed class QuantumConsoleState(
    bool connected,
    string? generatedDomain,
    int occupants,
    int points,
    bool randomized,
    bool ready,
    int scannerTier,
    int retriesLeft,
    List<QuantumConsoleDomainEntry> domains,
    List<QuantumConsoleAvatarEntry> avatars,
    bool broadcasting = false,
    bool broadcastOnCooldown = false) : BoundUserInterfaceState
{
    public bool Broadcasting = broadcasting;
    public bool BroadcastOnCooldown = broadcastOnCooldown;
    public bool Connected = connected;
    public string? GeneratedDomain = generatedDomain;
    public int Occupants = occupants;
    public int Points = points;
    public bool Randomized = randomized;
    public bool Ready = ready;
    public int ScannerTier = scannerTier;
    public int RetriesLeft = retriesLeft;
    public List<QuantumConsoleDomainEntry> Domains = domains;
    public List<QuantumConsoleAvatarEntry> Avatars = avatars;
}

[Serializable, NetSerializable]
public sealed class QuantumConsoleSetDomainMessage(string id) : BoundUserInterfaceMessage
{
    public string Id = id;
}

[Serializable, NetSerializable]
public sealed class QuantumConsoleRandomDomainMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class QuantumConsoleStopDomainMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class QuantumConsoleRefreshMessage : BoundUserInterfaceMessage;

/// <summary>toggle_broadcast(): трансляция аватаров на развлекательные экраны.</summary>
[Serializable, NetSerializable]
public sealed class QuantumConsoleBroadcastMessage : BoundUserInterfaceMessage;

#endregion

#region Визуал

[Serializable, NetSerializable]
public enum BitrunningVisuals : byte
{
    /// <summary>Строка-состояние спрайта (qserver_on, netpod_closed_active…).</summary>
    State,
    /// <summary>Байтфорж: частицы «злые» (красные).</summary>
    Angry,
    /// <summary>Байтфорж: вспышка материализации.</summary>
    Flicker,
}

[Serializable, NetSerializable]
public enum BitrunningVisualLayers : byte
{
    Base,
    Overlay,
    Particles,
}

#endregion

/// <summary>Действие «Информация о домене» (avatar_domain_info).</summary>
public sealed partial class BitrunningDomainInfoActionEvent : Content.Shared.Actions.InstantActionEvent;

/// <summary>Тревога «Домен завершён» — клик даёт безопасный выход.</summary>
public sealed partial class BitrunningSafeDisconnectAlertEvent : Content.Shared.Alert.BaseAlertEvent;

/// <summary>Сетевой маркер аватара: клиенту нужен для статического шума при входе.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BitrunningAvatarMarkerComponent : Component;

/// <summary>/obj/machinery/netpod: капсула подключения к домену.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NetpodComponent : Component
{
    [DataField]
    public string ContainerId = "netpod-occupant";

    /// <summary>state_open.</summary>
    [DataField, AutoNetworkedField]
    public bool Open = true;

    /// <summary>BASE_DISCONNECT_DAMAGE: урон мозгу при насильном отключении.</summary>
    [DataField]
    public float DisconnectDamage = 40f;

    /// <summary>netsuit: снаряжение аватара, выбранное кликом по нетподу.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<Content.Shared.Roles.StartingGearPrototype> Outfit = "BitrunnerGear";

    /// <summary>netsuit: выбранная в окне профессия — её снаряжение и лоадаут по умолчанию. Пусто — Outfit.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<Content.Shared.Roles.JobPrototype>? Job;

    /// <summary>copy_body: аватар похож на битраннера.</summary>
    [DataField, AutoNetworkedField]
    public bool CopyBody;

    [ViewVariables]
    public EntityUid? Server;

    [ViewVariables]
    public EntityUid? Avatar;

    [ViewVariables]
    public bool Connected;

    [ViewVariables]
    public TimeSpan? AutoOpenAt;

    [ViewVariables]
    public TimeSpan NextHeal;
}

[Serializable, NetSerializable]
public sealed partial class NetpodEnterDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class NetpodPryDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class HololadderDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class QuantumServerShutdownDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>Затаскивание себя в открытый нетпод (mouse_drop_receive).</summary>
public sealed class SharedNetpodSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NetpodComponent, Content.Shared.DragDrop.CanDropTargetEvent>(OnCanDrop);
    }

    private void OnCanDrop(Entity<NetpodComponent> ent, ref Content.Shared.DragDrop.CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.CanDrop = ent.Comp.Open && args.Dragged == args.User && HasComp<Content.Shared.Mobs.Components.MobStateComponent>(args.Dragged);
        args.Handled = true;
    }
}

#region Магазин и диски (Б2)

/// <summary>/datum/orderable_item/bitrunning_*: товар торгомата NexaCache за очки битраннинга.</summary>
[Prototype]
public sealed partial class BitrunningOrderPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Строкой, а не EntProtoId: часть товаров из других веток (Лаваленд, боевые искусства), без них заказ скрыт.</summary>
    [DataField(required: true)]
    public string Item = string.Empty;

    [DataField(required: true)]
    public int Cost;

    /// <summary>flair, tech.</summary>
    [DataField]
    public string Category = "tech";

    [DataField]
    public LocId? Description;
}

[Serializable, NetSerializable]
public enum BitrunningVendorUiKey : byte
{
    Key,
}

/// <summary>orderable_item в ui_static_data: цена уже умножена на cargo_cost_multiplier, как в TG.</summary>
[Serializable, NetSerializable]
public sealed class BitrunningVendorEntry(string id, string proto, string name, string description, string category, int cost)
{
    public string Id = id;
    public string Proto = proto;
    public string Name = name;
    public string Description = description;
    public string Category = category;
    public int Cost = cost;
}

/// <summary>ProduceConsole: каталог, корзина (grocery_list), очки владельца карты и откат заказа.</summary>
[Serializable, NetSerializable]
public sealed class BitrunningVendorState(
    int points,
    List<string> categories,
    List<BitrunningVendorEntry> entries,
    Dictionary<string, int> cart,
    int totalCost,
    bool offCooldown,
    float cargoCostMultiplier,
    float expressCostMultiplier,
    int cargoValue) : BoundUserInterfaceState
{
    public int Points = points;
    public List<string> Categories = categories;
    public List<BitrunningVendorEntry> Entries = entries;
    public Dictionary<string, int> Cart = cart;
    public int TotalCost = totalCost;
    public bool OffCooldown = offCooldown;
    public float CargoCostMultiplier = cargoCostMultiplier;
    public float ExpressCostMultiplier = expressCostMultiplier;
    public int CargoValue = cargoValue;
}

/// <summary>add_one / remove_one / cart_set: новое количество товара в корзине (0–20).</summary>
[Serializable, NetSerializable]
public sealed class BitrunningVendorCartMessage(string id, int amount) : BoundUserInterfaceMessage
{
    public string Id = id;
    public int Amount = amount;
}

/// <summary>purchase (рейсом карго) или express (сразу к покупателю).</summary>
[Serializable, NetSerializable]
public sealed class BitrunningVendorPurchaseMessage(bool express) : BoundUserInterfaceMessage
{
    public bool Express = express;
}

#region Нетпод

[Serializable, NetSerializable]
public enum NetpodUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class NetpodOutfitEntry(string id, string name)
{
    public string Id = id;
    public string Name = name;
}

/// <summary>NetpodOutfits: коллекция «Профессии» и выбранный netsuit.</summary>
[Serializable, NetSerializable]
public sealed class NetpodOutfitState(string? selected, List<NetpodOutfitEntry> jobs) : BoundUserInterfaceState
{
    public string? Selected = selected;
    public List<NetpodOutfitEntry> Jobs = jobs;
}

[Serializable, NetSerializable]
public sealed class NetpodSelectOutfitMessage(string id) : BoundUserInterfaceMessage
{
    public string Id = id;
}

#endregion

/// <summary>avatar_domain_info: окно AvatarHelp с подсказкой домена.</summary>
[Serializable, NetSerializable]
public sealed class BitrunningAvatarHelpEvent(string help) : EntityEventArgs
{
    public string Help = help;
}

/// <summary>/obj/item/disk/bitrunning: вариант программы — предметы и/или способности аватара.</summary>
[DataDefinition, Serializable]
public sealed partial class BitrunningDiskOption
{
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Строками: часть предметов из других веток (Лаваленд, боевые искусства), без них вариант скрыт.</summary>
    [DataField]
    public List<string> Items = new();

    [DataField]
    public List<EntProtoId> Actions = new();
}

/// <summary>Диск битраннинга: выбор делается один раз, загрузка — при входе в нетпод с диском при себе.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BitrunningDiskComponent : Component
{
    [DataField(required: true)]
    public List<BitrunningDiskOption> Options = new();

    [DataField, AutoNetworkedField]
    public int? Choice;
}

/// <summary>Действия-заклинания дисков (conjure/cheese, basic_heal, lightningbolt).</summary>
public sealed partial class BitrunningSummonCheeseActionEvent : Content.Shared.Actions.InstantActionEvent;

public sealed partial class BitrunningLesserHealActionEvent : Content.Shared.Actions.InstantActionEvent;

public sealed partial class BitrunningLightningBoltActionEvent : Content.Shared.Actions.EntityTargetActionEvent;

#endregion

#region Угрозы (Б3)

/// <summary>create_digital_aura(): красная «цифровая» подсветка и полупрозрачность глюка.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BitrunningDigitalAuraComponent : Component;

[Serializable, NetSerializable]
public sealed partial class BitrunningBreachDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>Ракетная установка нетгардиана (rapid_fire/netguardian).</summary>
public sealed partial class NetguardianRocketActionEvent : Content.Shared.Actions.WorldTargetActionEvent;

#endregion
