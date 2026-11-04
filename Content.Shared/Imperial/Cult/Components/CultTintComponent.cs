using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>Окраска/прозрачность спрайта (add_atom_colour, alpha): призраки культа, тёмный дух, тело духа.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CultTintComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;
}
