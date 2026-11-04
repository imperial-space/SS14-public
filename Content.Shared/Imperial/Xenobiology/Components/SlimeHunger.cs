using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>SLIME_HUNGER_*.</summary>
[Serializable, NetSerializable]
public enum SlimeHunger : byte
{
    None,
    Hungry,
    Starving,
}
