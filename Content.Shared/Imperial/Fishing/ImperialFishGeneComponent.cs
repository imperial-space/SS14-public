using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Инъектор черты рыбы (/obj/item/fish_gene).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFishGeneComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? Trait;
}
