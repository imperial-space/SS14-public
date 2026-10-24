using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

// ─── Трудовой лагерь ───────────────────────────────────────────────────────

/// <summary>card/id/advanced/prisoner: цель и набранные очки. Вешается на любую ID-карту.</summary>
[RegisterComponent]
public sealed partial class GulagPrisonerIdComponent : Component
{
    [DataField]
    public int Goal;

    [DataField]
    public int Points;
}

/// <summary>computer/prisoner/gulag_teleporter_computer.</summary>
[RegisterComponent]
public sealed partial class GulagConsoleComponent : Component
{
    public const string IdSlot = "gulag_id";

    /// <summary>default_goal.</summary>
    [DataField]
    public int DefaultGoal = 200;

    [ViewVariables]
    public EntityUid? Teleporter;

    [ViewVariables]
    public EntityUid? Beacon;
}

/// <summary>structure/gulag_beacon: точка прибытия заключённых.</summary>
[RegisterComponent]
public sealed partial class GulagBeaconComponent : Component;

/// <summary>stacking_machine/laborstacker: принимает листы и копит очки.</summary>
[RegisterComponent]
public sealed partial class LaborStackerComponent : Component
{
    /// <summary>SHEET_POINT_VALUE.</summary>
    [DataField]
    public int PointsPerSheet = 33;

    /// <summary>gulag_valid: какие листы засчитываются.</summary>
    [DataField]
    public List<ProtoId<StackPrototype>> AcceptedStacks = new();

    [ViewVariables]
    public int Points;

    /// <summary>Сданные листы по типам — отправляются на станцию вместе с освобождённым.</summary>
    [ViewVariables]
    public Dictionary<string, int> Stored = new();
}

/// <summary>labor_claim_console.</summary>
[RegisterComponent]
public sealed partial class LaborClaimConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? Stacker;

    [ViewVariables]
    public bool Emagged;

    /// <summary>say_cooldown.</summary>
    [ViewVariables]
    public TimeSpan NextSay;
}

/// <summary>labor_points_checker.</summary>
[RegisterComponent]
public sealed partial class LaborPointsCheckerComponent : Component;

/// <summary>gulag_item_reclaimer.</summary>
[RegisterComponent]
public sealed partial class GulagReclaimerComponent : Component
{
    public const string ContainerId = "gulag-reclaimer-items";

    [ViewVariables]
    public Container Items = default!;

    /// <summary>stored_items: чьи вещи лежат внутри.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, List<EntityUid>> Stored = new();

    [ViewVariables]
    public bool Emagged;
}

// ─── Консоль управления заключёнными и химимплант ──────────────────────────

/// <summary>computer/prisoner/management.</summary>
[RegisterComponent]
public sealed partial class PrisonerManagementConsoleComponent : Component
{
    public const string IdSlot = "prisoner_id";
}

/// <summary>implant/chem: 50 ед. реагентов, вводятся удалённо с консоли.</summary>
[RegisterComponent]
public sealed partial class ChemImplantComponent : Component
{
    public const string SolutionName = "implant";

    /// <summary>Раствор имплантера, который переливается в имплант при вживлении.</summary>
    [DataField]
    public string ImplanterSolution = "implanter";

    /// <summary>implant_sizes.</summary>
    [DataField]
    public List<int> Sizes = new() { 1, 5, 10 };

    [ViewVariables]
    public bool DeathTriggered;
}

// ─── Compli-o-nator ────────────────────────────────────────────────────────

public enum HailerAggression : byte
{
    GoodCop = 1,
    BadCop = 2,
    ShitCop = 3,
    Broken = 4,
}

/// <summary>clothing/mask/gas/sechailer.</summary>
[RegisterComponent]
public sealed partial class SecHailerComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionSecHailerHalt";

    [ViewVariables]
    public EntityUid? ActionEntity;

    /// <summary>aggressiveness.</summary>
    [DataField]
    public HailerAggression Aggression = HailerAggression.BadCop;

    /// <summary>Можно ли крутить ограничитель отвёрткой (у маски спецназа нельзя).</summary>
    [DataField]
    public bool Adjustable = true;

    [ViewVariables]
    public bool BrokenHailer;

    [ViewVariables]
    public bool Emagged;

    [ViewVariables]
    public int RecentUses;

    [ViewVariables]
    public TimeSpan? OveruseResetAt;

    [ViewVariables]
    public TimeSpan NextPhrase;
}

// ─── Мегафон ───────────────────────────────────────────────────────────────

/// <summary>obj/item/megaphone.</summary>
[RegisterComponent]
public sealed partial class MegaphoneComponent : Component
{
    [DataField]
    public string SpeechVerb = "ImperialMegaphone";

    [DataField]
    public string EmaggedSpeechVerb = "ImperialMegaphoneEmagged";

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Imperial/Security/megaphone.ogg");

    /// <summary>spamcheck: 5 секунд.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextUse;

    [ViewVariables]
    public bool Emagged;
}

/// <summary>Метка на том, кто держит мегафон.</summary>
[RegisterComponent]
public sealed partial class MegaphoneHolderComponent : Component;

// ─── Телескопическая дубинка ───────────────────────────────────────────────

/// <summary>melee/baton/telescopic.</summary>
[RegisterComponent]
public sealed partial class TelescopicBatonComponent : Component
{
    /// <summary>cooldown.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(4);

    /// <summary>knockdown_time.</summary>
    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(1.5);

    /// <summary>stamina_damage.</summary>
    [DataField]
    public float StaminaDamage = 55f;

    /// <summary>clumsy_knockdown_time.</summary>
    [DataField]
    public TimeSpan ClumsyKnockdownTime = TimeSpan.FromSeconds(15);

    /// <summary>dazed: на сколько противника ошеломляет.</summary>
    [DataField]
    public TimeSpan DazeTime = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier StunSound = new SoundPathSpecifier("/Audio/Imperial/MartialArts/woodhit.ogg");

    [ViewVariables]
    public TimeSpan NextStun;
}

// ─── Электростул ───────────────────────────────────────────────────────────

/// <summary>assembly/shock_kit.</summary>
[RegisterComponent]
public sealed partial class ShockKitComponent : Component
{
    [DataField]
    public EntProtoId ChairProto = "ImperialElectricChair";
}

/// <summary>chair/e_chair + component/electrified_buckle.</summary>
[RegisterComponent]
public sealed partial class ElectricChairComponent : Component
{
    public const string KitContainer = "shock-kit";

    /// <summary>Из какого стула собран — в него он и превращается обратно.</summary>
    [DataField]
    public string? OriginalChair;

    /// <summary>shock_on_loop.</summary>
    [DataField]
    public bool Active;

    /// <summary>shock_loop_time.</summary>
    [DataField]
    public TimeSpan LoopTime = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextShock;

    [ViewVariables]
    public TimeSpan? ShockVisualUntil;
}
