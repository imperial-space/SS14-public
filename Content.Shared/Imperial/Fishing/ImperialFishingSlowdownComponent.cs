using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Тяжёлая рыба или полный переносной аквариум замедляют того, кто их держит (slowdown / GET_FISH_SLOWDOWN).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFishingSlowdownComponent : Component
{
    /// <summary>Замедление в единицах TG (добавка к задержке шага).</summary>
    [DataField, AutoNetworkedField]
    public float Slowdown;
}
