using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared.Imperial.Heretic.Prototypes;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedHereticSystem))]
public sealed partial class HereticComponent : Component
{
    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon = "HereticFaction";
    [AutoNetworkedField]
    public int KnowledgePoints = 1;

    [AutoNetworkedField]
    public int TotalKnowledgeGained = 0;

    // Sum of proto.Cost for every node bought in the shop (gifts count at their original cost)
    [AutoNetworkedField]
    public int TotalShopCostLearned = 0;

    [AutoNetworkedField]
    public HereticPath CurrentPath = HereticPath.General;

    [AutoNetworkedField]
    public List<ProtoId<HereticKnowledgePrototype>> ResearchedKnowledge = new();

    [AutoNetworkedField]
    public List<EntityUid> GrantedActions = new();

    // Passive knowledge gain
    [DataField]
    public float PassiveGainIntervalSeconds = 1200f;

    [AutoNetworkedField]
    public float PassiveGainAccumulator = 0f;

    // Sacrifice count for objective tracking
    [AutoNetworkedField]
    public int SacrificeCount = 0;

    // Required values for ascension tasks display
    [AutoNetworkedField]
    public int RequiredSacrifices = 5;
    [AutoNetworkedField]
    public int RequiredKnowledge = 0;

    // Named personal sacrifice targets assigned at round start
    [AutoNetworkedField]
    public List<EntityUid> NamedTargets = new();

    // High-value (head of staff) sacrifice count for objective tracking
    [AutoNetworkedField]
    public int HighValueSacrificeCount = 0;

    // Mansus ritual summon count for objective tracking
    [AutoNetworkedField]
    public int SummonCount = 0;

    // Corpses that qualify for ascension path requirements
    [AutoNetworkedField]
    public int AscensionCorpsesQualified = 0;

    // Accumulator for aura tick
    [AutoNetworkedField]
    public float AuraAccumulator = 0f;

    // Holder entity for the Knowledge BUI
    [AutoNetworkedField]
    public EntityUid BuiHolder = EntityUid.Invalid;

    // Mansus book entity, used to drive its open/close sprite animation
    [AutoNetworkedField]
    public EntityUid MansusBook = EntityUid.Invalid;

    // Current blade entity
    [AutoNetworkedField]
    public EntityUid CurrentBlade = EntityUid.Invalid;

    [AutoNetworkedField]
    public bool FeastOfOwlsUsed = false;

    // Whether the blade has been upgraded via a blade_upgrade knowledge node
    [AutoNetworkedField]
    public bool BladeUpgraded = false;

    // Whether the ascension action has already been granted (prevents double-grant)
    [AutoNetworkedField]
    public bool AscensionTriggered = false;

    [AutoNetworkedField]
    public bool AscensionDenied = false;

    // Admin bypass: skip objectives check when researching ascension knowledge
    [AutoNetworkedField]
    public bool AscensionBypass = false;

    // Number of Sundered Blades crafted via The Cutting Edge transmutation (max 5 per round)
    [AutoNetworkedField]
    public int SunderedBladeCraftCount = 0;

    // Number of Key Blades crafted via Lockwielder's Secret transmutation (max 2 per round)
    [AutoNetworkedField]
    public int KeyBladeCraftCount = 0;

    // Number of Shattered Risen created via Shattered Ritual (max 1 per round)
    [AutoNetworkedField]
    public int ShatteredGhoulCount = 0;

    // Total orbiting blades ever summoned (used for SS13-like craft limit)
    [AutoNetworkedField]
    public int BladesCreated = 0;

    // When true, blade breaking on UseInHand is disabled (activated after gaining aura)
    [AutoNetworkedField]
    public bool UnlimitedBlades = false;

    // Decorative knives currently orbiting this heretic (Mark of the Blade / Furious Steel)
    [AutoNetworkedField]
    public List<EntityUid> OrbitingBlades = new();

    // Path passive progression: 0 = no path, 1 = path chosen, 2 = path robe worn, 3 = Pact ritual completed
    [AutoNetworkedField]
    public int PassiveLevel = 0;

    [AutoNetworkedField]
    public int RealignmentLevel = 0;


    // Knowledge shop level (0 = shop locked; 1-5 unlocked after researching each path node)
    public int ShopLevel = 0;
    public int MaxPathTier = -1;
    public List<HereticGiftGroup> PendingGiftGroups = new();
}
