using System.Linq;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.EntityLayer;


/// <summary>
/// Z-levels for entities. Handles interactions with else entities.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EntityLayerComponent : Component
{
    /// <summary>
    /// Replicate current mask and layer to entity we containing
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ReplicateMaskOnContainedChildren = true;

    /// <summary>
    /// Should we add masks to children when adding this component. Set this to true if you don't add this component to the entity when it spawns.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RecursiveUpdateContainingChildrenOnAdd = false;

    /// <summary>
    /// Should us damage explosions
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanDamageByExplosions = false;

    /// <summary>
    /// The time after which the entity that received the layer and mask from us will lose them when thrown or excluded from the container
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ThrowLayerRemoveTime = TimeSpan.Zero;

    /// <summary>
    /// The time it takes us to add layers to the entity we are pulling
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan PullingChildLayerAddTime = TimeSpan.Zero;

    /// <summary>
    /// The time it takes us to remove layers to the entity we are pulling
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan PullingChildLayerRemoveTime = TimeSpan.Zero;

    /// <summary>
    /// Components that will be added to our entity and its descendants when this component is added.
    /// </summary>
    [DataField]
    public ComponentRegistry EffectComponents = new();

    /// <summary>
    /// Layers we can interact with
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityLayerGroups> CollideMasks = new() { EntityLayerGroups.Overworld };

    /// <summary>
    /// The layer on which the entity is located
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityLayerGroups> CollideLayers = new() { EntityLayerGroups.Overworld };


    /// <summary>
    /// Cached entities that had no layers when they were picked up
    /// </summary>
    [ViewVariables, NonSerialized]
    public List<EntityUid> WithoutLayerComps = new();

    /// <summary>
    /// If the entity had a layer before being picked up, its old masks are cached and will be assigned back to it when it leaves the container.
    /// </summary>
    [ViewVariables, NonSerialized]
    public Dictionary<EntityUid, HashSet<EntityLayerGroups>> CachedChildMasks = new();

    /// <summary>
    /// If the entity had a layer before being picked up, its old layers are cached and will be assigned back to it when it leaves the container.
    /// </summary>
    [ViewVariables, NonSerialized]
    public Dictionary<EntityUid, HashSet<EntityLayerGroups>> CachedChildLayers = new();


    /// <summary>
    /// Entities that we need to return their old layer component to over time
    /// </summary>
    [ViewVariables, NonSerialized]
    public Dictionary<EntityUid, TimeSpan> RemoveStack = new();

    /// <summary>
    /// Entities that we need to assign layers to after a certain amount of time
    /// </summary>
    [ViewVariables, NonSerialized]
    public Dictionary<EntityUid, TimeSpan> AddStack = new();


    /// <summary>
    /// Total entity mask. Compared using bitwise "and" and if the number is not zero, then interaction occurs
    /// <para>
    /// https://learn.microsoft.com/ru-ru/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators#logical-and-operator-
    /// </para>
    /// </summary>
    public int Mask => (int)CollideMasks.Aggregate((acc, el) => el | acc);

    /// <summary>
    /// Total entity layer. Compared using bitwise "and" and if the number is not zero, then interaction occurs
    /// <para>
    /// https://learn.microsoft.com/ru-ru/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators#logical-and-operator-
    /// </para>
    /// </summary>
    public int Layer => (int)CollideLayers.Aggregate((acc, el) => el | acc);
}
