using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Сервер → клиент: открыть окно объявления (последний текст и сколько ждать).</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiAnnouncementOpenEvent(string lastMessage, float cooldown) : EntityEventArgs
{
    public string LastMessage = lastMessage;
    public float Cooldown = cooldown;
}
