using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticScorchedMantleComponent : Component
{
    [DataField]
    public EntProtoId ToggleAction = "ActionToggleScorchedMantleFlames";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    /// <summary>True while passive flame generation is enabled.</summary>
    [DataField, AutoNetworkedField]
    public bool FlamesActive;

    /// <summary>EntityUid of the current wearer, set on equip, cleared on unequip.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Wearer;

    /// <summary>Interval between passive fire stack additions.</summary>
    [DataField]
    public TimeSpan FlameInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan NextFlameTime;
}
