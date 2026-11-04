using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialAquariumBoundUserInterfaceState : BoundUserInterfaceState
{
    public ImperialFishFluid Fluid;
    public float Temperature;
    public float MinTemperature;
    public float MaxTemperature;
    public ImperialAquariumMode Mode;
    public float FeedingIntervalMinutes;
    public List<ImperialAquariumFishEntry> Fish = new();
    public List<ImperialAquariumPropEntry> Props = new();
}
