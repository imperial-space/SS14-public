using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>TS_DAMAGE_*: как ИИ выбирает цели.</summary>
public enum TerrorSpiderTargetMethod : byte
{
    Simple,
    Poison,
    Brute,
}

/// <summary>Особая атака spider_specialattack().</summary>
public enum TerrorSpiderAttackType : byte
{
    None,
    Red,
    Gray,
    Green,
    Black,
    White,
    Prince,
}

/// <summary>mob/living/simple_animal/hostile/poison/terror_spider.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderComponent : Component
{
    /// <summary>spider_tier: TS_TIER_1..5.</summary>
    [DataField]
    public int Tier = 1;

    [DataField]
    public TerrorSpiderAttackType Attack = TerrorSpiderAttackType.None;

    [DataField]
    public TerrorSpiderTargetMethod TargetMethod = TerrorSpiderTargetMethod.Simple;

    /// <summary>melee_damage_lower / melee_damage_upper.</summary>
    [DataField]
    public float MeleeLower = 15;

    [DataField]
    public float MeleeUpper = 20;

    // Регенерация.
    [DataField]
    public float RegenPoints;

    [DataField]
    public float RegenPointsMax = 100;

    [DataField]
    public float RegenPointsPerTick = 1;

    [DataField]
    public float RegenPointsPerKill = 90;

    [DataField]
    public float RegenPointsPerHp = 3;

    [DataField]
    public float RegenPointsPerJelly = 120;

    [DataField]
    public bool Degenerate;

    /// <summary>web_type; null — паук не плетёт паутину.</summary>
    [DataField]
    public EntProtoId? Web = "TerrorWeb";

    /// <summary>delay_web (40 = 4 секунды).</summary>
    [DataField]
    public TimeSpan WebDelay = TimeSpan.FromSeconds(4);

    // ИИ.
    [DataField]
    public bool AiSpinsWebs = true;

    [DataField]
    public bool AiBreakLights = true;

    [DataField]
    public bool AiVentcrawls = true;

    [DataField]
    public bool AiVentBreaker;

    [DataField]
    public int IdleVentcrawlChance = 15;

    [DataField]
    public TimeSpan VentcrawlCombatFrequency = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan VentcrawlIdleFrequency = TimeSpan.FromMinutes(15);

    /// <summary>environment_smash = ENVIRONMENT_SMASH_RWALLS: ломает стены атакой.</summary>
    [DataField]
    public bool SmashWalls;

    /// <summary>ai_playercontrol_allowtype.</summary>
    [DataField]
    public bool PlayerControllable = true;

    /// <summary>spider_role_summary / spider_intro_text.</summary>
    [DataField]
    public LocId RoleSummary = "terror-spider-summary-red";

    [DataField]
    public LocId IntroText = "terror-spider-intro-red";

    /// <summary>loudspeaker: говорит громко в разуме улья.</summary>
    [DataField]
    public bool Loudspeaker;

    /// <summary>Дополнительные действия типа (кладка, вентиляция, обзор...).</summary>
    [DataField]
    public List<EntProtoId> Actions = new();

    // Рабочие переменные.
    [ViewVariables]
    public float Fed;

    [ViewVariables]
    public int KillCount;

    [ViewVariables]
    public EntityUid? Queen;

    [ViewVariables]
    public EntityUid? Mother;

    [ViewVariables]
    public HashSet<EntityUid> Enemies = new();

    [ViewVariables]
    public TimeSpan CreatedAt;

    [ViewVariables]
    public TimeSpan NextLife;

    [ViewVariables]
    public TimeSpan? DecomposeAt;

    [ViewVariables]
    public bool HasDied;

    [ViewVariables]
    public bool IntroShown;

    [ViewVariables]
    public bool Busy;

    [ViewVariables]
    public EntityUid? CocoonTarget;

    [ViewVariables]
    public TimeSpan LastWeb;

    [ViewVariables]
    public TimeSpan LastBreakLight;

    [ViewVariables]
    public TimeSpan LastVentcrawl = TimeSpan.FromMinutes(-15);

    [ViewVariables]
    public TimeSpan LastCocoonObject;

    /// <summary>Наблюдаемый через удалённый обзор паук.</summary>
    [ViewVariables]
    public EntityUid? Watching;

    /// <summary>Паук-ИИ ползёт по вентиляции и вылезет здесь.</summary>
    [ViewVariables]
    public (EntityUid Vent, TimeSpan At)? VentTravel;
}

/// <summary>Красный: enrage-ступени урона.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderRedComponent : Component
{
    [ViewVariables]
    public int Enrage;
}

/// <summary>Серый: невидим на своей паутине, массовая паутина ИИ.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderGrayComponent : Component
{
    [DataField]
    public int MasswebChance = 10;
}

/// <summary>Зелёный: кладка за каждые два обмотанных трупа.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderGreenComponent : Component
{
    [DataField]
    public int FeedingsToLay = 2;
}

/// <summary>Фиолетовый: страж королевы, вырождается вдали от неё.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderPurpleComponent : Component
{
    [DataField]
    public float MaxQueenRange = 20;

    [ViewVariables]
    public bool QueenVisible = true;

    [ViewVariables]
    public int CyclesNoQueen;

    [ViewVariables]
    public int LifeTicks;
}

/// <summary>Мать: королевское желе, перенос паучат, подогрев яиц.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderMotherComponent : Component
{
    [DataField]
    public float JellyCost = 100;
}

/// <summary>Принц: таран princely_charge.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderPrinceComponent : Component
{
    [ViewVariables]
    public bool Charging;
}

/// <summary>Королева, а также принцесса и императрица (подтипы queen).</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderQueenComponent : Component
{
    [DataField]
    public bool Princess;

    [DataField]
    public bool Empress;

    /// <summary>spider_spawnfrequency (1200 = 120 секунд).</summary>
    [DataField]
    public TimeSpan SpawnFrequency = TimeSpan.FromSeconds(120);

    /// <summary>nestfrequency (300 = 30 секунд).</summary>
    [DataField]
    public TimeSpan NestFrequency = TimeSpan.FromSeconds(30);

    [DataField]
    public int MaxPerNest = 35;

    /// <summary>spider_max_children принцессы.</summary>
    [DataField]
    public int MaxChildren = 8;

    [DataField]
    public int CanLay = 5;

    [DataField]
    public bool HasNested;

    [ViewVariables]
    public int EggsLaid;

    [ViewVariables]
    public TimeSpan LastSpawn;

    [ViewVariables]
    public TimeSpan LastNestSetup;

    [ViewVariables]
    public int NestStep;

    [ViewVariables]
    public EntityUid? NestVent;
}

/// <summary>obj/structure/spider/terrorweb.</summary>
[RegisterComponent]
public sealed partial class TerrorWebComponent : Component
{
    [DataField]
    public TerrorWebAbility Ability = TerrorWebAbility.None;

    /// <summary>Паутина матери не пропускает паучат.</summary>
    [DataField]
    public bool BlocksSpiderlings;
}

public enum TerrorWebAbility : byte
{
    None,
    Green,
    Black,
    White,
}

/// <summary>obj/structure/spider/eggcluster/terror_eggcluster.</summary>
[RegisterComponent]
public sealed partial class TerrorEggClusterComponent : Component
{
    [DataField]
    public EntProtoId? SpiderlingType;

    [DataField]
    public int SpiderlingNumber = 1;

    [DataField]
    public float AmountGrown;

    [ViewVariables]
    public EntityUid? Queen;

    [ViewVariables]
    public EntityUid? Mother;

    [ViewVariables]
    public HashSet<EntityUid> Enemies = new();
}

/// <summary>mob/living/basic/spiderling/terror_spiderling.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderlingComponent : Component
{
    [DataField]
    public EntProtoId? GrowAs;

    [DataField]
    public float AmountGrown;

    [DataField]
    public bool Stillborn;

    [ViewVariables]
    public EntityUid? Queen;

    [ViewVariables]
    public EntityUid? Mother;

    [ViewVariables]
    public HashSet<EntityUid> Enemies = new();
}

/// <summary>obj/structure/spider/royaljelly.</summary>
[RegisterComponent]
public sealed partial class TerrorRoyalJellyComponent : Component;

/// <summary>obj/item/organ/internal/body_egg/terror_eggs: яйца белого паука в носителе.</summary>
[RegisterComponent]
public sealed partial class TerrorEggsInfectionComponent : Component
{
    [ViewVariables]
    public int CycleNum;

    [ViewVariables]
    public float EggProgress;

    [DataField]
    public float EggProgressPerHatch = 90;

    [ViewVariables]
    public int EggsHatched;
}

/// <summary>AdjustEyeBlurry: временное размытие зрения.</summary>
[RegisterComponent]
public sealed partial class TerrorEyeBlurComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}

/// <summary>Отслеживание яда чёрного паука (terror_black_toxin) в крови.</summary>
[RegisterComponent]
public sealed partial class TerrorBlackVenomComponent : Component;

/// <summary>Кокон паука: внутри труп или предметы.</summary>
[RegisterComponent]
public sealed partial class TerrorCocoonComponent : Component;

/// <summary>Паук, спрятанный в трубах при перемещении ИИ по вентиляции.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderVentTravelComponent : Component
{
    [ViewVariables]
    public EntityCoordinates Origin;
}
