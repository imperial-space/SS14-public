using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Сервер → клиент: киборги станции ИИ.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiBorgListEvent(List<ImperialAiBorgEntry> borgs) : EntityEventArgs
{
    public List<ImperialAiBorgEntry> Borgs = borgs;
}
