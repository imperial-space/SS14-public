using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>mob/living/basic/illusion: копирует внешний вид Source.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CultIllusionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Source;
}
