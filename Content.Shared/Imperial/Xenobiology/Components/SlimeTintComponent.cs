using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>add_atom_colour(): временная окраска существа эффектами кроссбридинга.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SlimeTintComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, Color> Tints = new();
}
