using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Клиент → сервер: текст объявления.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiAnnouncementEvent(string message) : EntityEventArgs
{
    public string Message = message;
}
