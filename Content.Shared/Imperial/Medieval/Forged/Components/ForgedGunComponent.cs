using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Magic.Mana;

[RegisterComponent, NetworkedComponent]
public sealed partial class ForgedGunComponent : Component
{
    [DataField("hungerCost")]
    public float HungerCost = 2f;
}
