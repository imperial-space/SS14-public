using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>Луч (Beam) от источника длиной Length тайлов вдоль локального "вверх".</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CultBeamComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Length = 1f;
}
