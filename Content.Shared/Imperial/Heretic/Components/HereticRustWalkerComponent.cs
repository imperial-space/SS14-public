using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticRustWalkerComponent : Component
{
    [DataField]
    public float HealInterval = 1f;

    [DataField]
    public float HealAmount = 3f;

    public float HealAccum;

    /// <summary>Спрайт при движении/стоянии лицом на север.</summary>
    [DataField]
    public string NorthState = "rust_walker_n";

    /// <summary>Спрайт при движении/стоянии в остальных направлениях.</summary>
    [DataField]
    public string SouthState = "rust_walker_s";

    /// <summary>Клиентское: двигался ли ходок при последнем обновлении спрайта.</summary>
    [ViewVariables]
    public bool VisualMoving;

    /// <summary>Клиентское: направление при последнем обновлении спрайта.</summary>
    [ViewVariables]
    public Direction? VisualDirection;
}
