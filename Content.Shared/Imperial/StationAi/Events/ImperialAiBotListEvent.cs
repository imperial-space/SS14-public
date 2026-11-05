using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

[Serializable, NetSerializable]
public sealed class ImperialAiBotListEvent(List<ImperialAiBotEntry> bots) : EntityEventArgs
{
    public List<ImperialAiBotEntry> Bots = bots;
}
