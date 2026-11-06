using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialAquariumSetModeMessage(ImperialAquariumMode mode) : BoundUserInterfaceMessage
{
    public readonly ImperialAquariumMode Mode = mode;
}
