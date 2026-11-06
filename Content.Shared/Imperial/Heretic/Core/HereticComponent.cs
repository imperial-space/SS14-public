using Content.Shared.Imperial.Heretic.Prototypes;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.Core;

[RegisterComponent, AutoGenerateComponentPause, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedHereticSystem))]
public sealed partial class HereticComponent : Component
{
    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon = "HereticFaction";
    [ViewVariables]
    [AutoNetworkedField]
    public int KnowledgePoints = 1;

    [ViewVariables]
    [AutoNetworkedField]
    public int TotalKnowledgeGained = 0;

    // Sum of proto.Cost for every node bought in the shop (gifts count at their original cost)
    [ViewVariables]
    [AutoNetworkedField]
    public int TotalShopCostLearned = 0;

    [ViewVariables]
    [AutoNetworkedField]
    public HereticPath CurrentPath = HereticPath.General;

    [ViewVariables]
    [AutoNetworkedField]
    public List<ProtoId<HereticKnowledgePrototype>> ResearchedKnowledge = new();

    [ViewVariables]
    [AutoNetworkedField]
    public List<EntityUid> GrantedActions = new();

    // Passive knowledge gain
    [DataField]
    public TimeSpan PassiveGainIntervalSeconds = TimeSpan.FromSeconds(1200f);

    [ViewVariables, AutoPausedField, AutoNetworkedField]
    public TimeSpan NextPassiveGain;

    // Sacrifice count for objective tracking
    [ViewVariables]
    [AutoNetworkedField]
    public int SacrificeCount = 0;

    // Required values for ascension tasks display
    [ViewVariables]
    [AutoNetworkedField]
    public int RequiredSacrifices = 5;
    [ViewVariables]
    [AutoNetworkedField]
    public int RequiredKnowledge = 0;

    // Named personal sacrifice targets assigned at round start
    [ViewVariables]
    [AutoNetworkedField]
    public List<EntityUid> NamedTargets = new();

    // High-value (head of staff) sacrifice count for objective tracking
    [ViewVariables]
    [AutoNetworkedField]
    public int HighValueSacrificeCount = 0;

    // Mansus ritual summon count for objective tracking
    [ViewVariables]
    [AutoNetworkedField]
    public int SummonCount = 0;

    // Corpses that qualify for ascension path requirements
    [ViewVariables]
    [AutoNetworkedField]
    public int AscensionCorpsesQualified = 0;

    // Accumulator for aura tick
    [ViewVariables, AutoPausedField]
    public TimeSpan NextAuraTick;

    // Holder entity for the Knowledge BUI
    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid? BuiHolder;

    // Mansus book entity, used to drive its open/close sprite animation
    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid? MansusBook;

    // Current blade entity
    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid? CurrentBlade;

    [ViewVariables]
    [AutoNetworkedField]
    public bool FeastOfOwlsUsed = false;

    // Whether the blade has been upgraded via a blade_upgrade knowledge node
    [ViewVariables]
    [AutoNetworkedField]
    public bool BladeUpgraded = false;

    // Whether the ascension action has already been granted (prevents double-grant)
    [ViewVariables]
    [AutoNetworkedField]
    public bool AscensionTriggered = false;

    [ViewVariables]
    [AutoNetworkedField]
    public bool AscensionDenied = false;

    // Admin bypass: skip objectives check when researching ascension knowledge
    [ViewVariables]
    [AutoNetworkedField]
    public bool AscensionBypass = false;

    // Number of Sundered Blades crafted via The Cutting Edge transmutation (max 5 per round)
    [ViewVariables]
    [AutoNetworkedField]
    public int SunderedBladeCraftCount = 0;

    // Number of Key Blades crafted via Lockwielder's Secret transmutation (max 2 per round)
    [ViewVariables]
    [AutoNetworkedField]
    public int KeyBladeCraftCount = 0;

    // Number of Shattered Risen created via Shattered Ritual (max 1 per round)
    [ViewVariables]
    [AutoNetworkedField]
    public int ShatteredGhoulCount = 0;

    // Total orbiting blades ever summoned (used for SS13-like craft limit)
    [ViewVariables]
    [AutoNetworkedField]
    public int BladesCreated = 0;

    // When true, blade breaking on UseInHand is disabled (activated after gaining aura)
    [ViewVariables]
    [AutoNetworkedField]
    public bool UnlimitedBlades = false;

    // Decorative knives currently orbiting this heretic (Mark of the Blade / Furious Steel)
    [ViewVariables]
    [AutoNetworkedField]
    public List<EntityUid> OrbitingBlades = new();

    // Path passive progression: 0 = no path, 1 = path chosen, 2 = path robe worn, 3 = Pact ritual completed
    [ViewVariables]
    [AutoNetworkedField]
    public int PassiveLevel = 0;

    [ViewVariables]
    [AutoNetworkedField]
    public int RealignmentLevel = 0;

    // Knowledge shop level (0 = shop locked; 1-5 unlocked after researching each path node)
    [ViewVariables]
    public int ShopLevel = 0;
    [ViewVariables]
    public int MaxPathTier = -1;
    [ViewVariables]
    public List<HereticGiftGroup> PendingGiftGroups = new();
}
