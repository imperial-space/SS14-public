using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Сервер → клиент: окно тревог станции.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiAlertsEvent(List<ImperialAiAlarmEntry> alarms) : EntityEventArgs
{
    public List<ImperialAiAlarmEntry> Alarms = alarms;
}
