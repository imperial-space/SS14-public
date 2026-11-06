using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// temp_visual/cult/rune_spawn: проявляется (alpha 0 → 255) и сжимается с поворотом за Duration.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CultRuneSpawnEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(6f);

    /// <summary>turnedness: 179 — против часовой, 181 — по часовой.</summary>
    [DataField, AutoNetworkedField]
    public float Turn = 179f;
}
