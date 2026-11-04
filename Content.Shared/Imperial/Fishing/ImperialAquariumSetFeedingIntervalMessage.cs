using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialAquariumSetFeedingIntervalMessage(float minutes) : BoundUserInterfaceMessage
{
    public readonly float Minutes = minutes;
}
