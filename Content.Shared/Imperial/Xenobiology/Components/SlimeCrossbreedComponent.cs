using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>obj/item/slimecross: скрещенный экстракт.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeCrossbreedComponent : Component
{
    /// <summary>colour.</summary>
    [DataField(required: true)]
    public ProtoId<SlimeTypePrototype> SlimeType;

    /// <summary>effect: модификация (burning, charged...).</summary>
    [DataField(required: true)]
    public string Effect = string.Empty;

    /// <summary>effect_desc.</summary>
    [DataField]
    public LocId? EffectDesc;
}
