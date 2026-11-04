using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>Цель «Импульса» мастера выбрана (видит только мастер).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CultPulseSelectedComponent : Component
{
    [ViewVariables, AutoNetworkedField] public EntityUid Master;
}
