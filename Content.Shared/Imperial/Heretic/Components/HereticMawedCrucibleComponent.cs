using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticMawedCrucibleComponent : Component
{
    [DataField]
    public int MaxCharges = 5;

    [AutoNetworkedField]
    public int Charges = 5;

    [DataField]
    public float SoulDuration = 60f;

    [DataField]
    public float SoulCooldown = 120f;

    [DataField]
    public float ClarityDuration = 60f;

    [DataField]
    public float MarshalDuration = 60f;

    [AutoNetworkedField]
    public TimeSpan CooldownEnd = TimeSpan.Zero;
}
