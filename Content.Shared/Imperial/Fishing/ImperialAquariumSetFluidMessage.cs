using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialAquariumSetFluidMessage(ImperialFishFluid fluid) : BoundUserInterfaceMessage
{
    public readonly ImperialFishFluid Fluid = fluid;
}
