using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>SLIME_MOOD_*: лицо слайма.</summary>
[Serializable, NetSerializable]
public enum SlimeMood : byte
{
    None,
    Angry,
    Mischievous,
    Pout,
    Sad,
    Smile,
    Cat,
}
