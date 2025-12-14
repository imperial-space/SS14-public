using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Illitid;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IllitidFlashedComponent : Component
{
    [ViewVariables, DataField("strength"), AutoNetworkedField]
    public float Strength = 0.15f;
}

