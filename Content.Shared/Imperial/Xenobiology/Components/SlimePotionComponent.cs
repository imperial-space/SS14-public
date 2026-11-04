using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>obj/item/slimepotion.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimePotionComponent : Component
{
    [DataField(required: true)]
    public SlimePotionKind Kind;

    /// <summary>uses (у зелья огнеупорности их три).</summary>
    [DataField, AutoNetworkedField]
    public int Uses = 1;

    /// <summary>Зелье применяется только к слаймам (slimepotion/slime).</summary>
    [DataField]
    public bool SlimeOnly;

    /// <summary>being_used / prompted.</summary>
    [ViewVariables]
    public bool InUse;
}
