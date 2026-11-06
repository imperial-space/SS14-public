using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Клиент → сервер: огласить отмеченные законы на канале (пустой канал — вслух).</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiStateLawsEvent(List<int> laws, string? channel) : EntityEventArgs
{
    public List<int> Laws = laws;
    public string? Channel = channel;
}
