using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

/// <summary>/obj/effect/pod_landingzone: метка посадки, вращается до прилёта.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialPodLandingZoneComponent : Component
{
    /// <summary>Когда метка появилась и когда капсула сядет (transit + falling).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan Arrival;

    /// <summary>Прототип капсулы.</summary>
    [DataField]
    public EntProtoId Pod = "ImperialSupplyPod";

    /// <summary>Что положить в капсулу (ящик заказа).</summary>
    [DataField]
    public EntProtoId? Cargo;

    /// <summary>Готовые сущности, которые сядут в капсулу (особенности станции: пицца в бар, экипаж на капсулах).</summary>
    [ViewVariables]
    public List<EntityUid> Contents = new();

    /// <summary>delays[POD_TRANSIT].</summary>
    [DataField]
    public float Transit = 3f;

    [ViewVariables]
    public bool SoundPlayed;

    [ViewVariables]
    public EntityUid? PodEntity;
}
