using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology.Components;

[Serializable, NetSerializable]
public enum SlimeLifeStage : byte
{
    Baby,
    Adult,
}
