using Content.Shared.Actions.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.CustomChaplain.Components;

/// <summary>
/// Component for the Bible that allows it to be recalled to its owner's hand.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialBibleComponent : Component
{
    /// <summary>
    /// The owner of this bible who can recall it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Owner;

    /// <summary>
    /// Whether this bible has been bound to an owner.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsBound = false;

    /// <summary>
    /// The recall action entity for this bible.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? RecallActionEntity;
}
