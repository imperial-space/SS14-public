using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

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

    [DataField]
    public TimeSpan WrapDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan VentSmashActionDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan SpecialAttackGreenAmountDelay = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan SpecialAttackGreenMaxDelay = TimeSpan.FromSeconds(120);

    [DataField]
    public TimeSpan SpecialAttackWhiteTime = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan SpecialAttackPrinceTime = TimeSpan.FromSeconds(10);

    [DataField]
    public float WrapDoAfterRange = 1.5f;

    [DataField]
    public float WrapDoAfterRange2 = 0.4f;

    [DataField]
    public float VentSmashActionRange = 1.5f;

    [DataField]
    public float VentSmashDoAfterRange = 1.5f;

    [DataField]
    public float AutomatedMovementRange = 1.5f;

    [DataField]
    public float AutomatedMovementRange2 = 10f;

    [DataField]
    public float CocoonTargetRange = 1.5f;

    [DataField]
    public float SeekCocoonTargetRange = 10f;

    [DataField]
    public float QueenAiRange = 10f;

    [DataField]
    public float QueenAiChance = 0.2f;

    [DataField]
    public float RetaliateRange = 7f;

    [DataField]
    public float SpecialAttackBlackAmount = 30f;

    [DataField]
    public float SpecialAttackBlackDistance = 3f;

    [DataField]
    public float SpecialAttackWhiteDistance = 2f;

    [DataField]
    public float CanLayGreenEggsRange = 0.4f;

    [DataField]
    public float QueenDeathChance = 0.5f;

    [DataField]
    public float PurpleLifeRange = 10f;
}
