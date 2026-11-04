using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>Предмет или одежда уже обработаны зельем (TRAIT_SPEED_POTIONED, огнеупорность).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimePotionedComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Speed;

    [DataField, AutoNetworkedField]
    public bool Fireproof;

    /// <summary>slimepotion/lavaproof.</summary>
    [DataField, AutoNetworkedField]
    public bool Lavaproof;

    /// <summary>slimepotion/spaceproof.</summary>
    [DataField, AutoNetworkedField]
    public bool Spaceproof;
}
