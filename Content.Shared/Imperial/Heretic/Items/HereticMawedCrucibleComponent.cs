using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticMawedCrucibleComponent : Component
{
    [DataField]
    public int MaxCharges = 5;

    [ViewVariables]
    [AutoNetworkedField]
    public int Charges = 5;

    [DataField]
    public TimeSpan SoulDuration = TimeSpan.FromSeconds(60f);

    [DataField]
    public TimeSpan SoulCooldown = TimeSpan.FromSeconds(120f);

    [DataField]
    public TimeSpan ClarityDuration = TimeSpan.FromSeconds(60f);

    [DataField]
    public TimeSpan MarshalDuration = TimeSpan.FromSeconds(60f);

    [ViewVariables]
    [AutoNetworkedField]
    public TimeSpan CooldownEnd = TimeSpan.Zero;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);
}
