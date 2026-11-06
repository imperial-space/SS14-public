using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Security;

/// <summary>
/// component/shielded: на носителе гамма-РИГа виден энергощит, пока у него есть заряды.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class EnergyShieldWearerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>shield_icon: состояние в energy_shield.rsi («shield» у гаммы, «shield-red» у Синдиката).</summary>
    [DataField, AutoNetworkedField]
    public string State = "shield";
}
