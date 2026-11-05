using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Сервер → клиенты: проиграть слова VOX по очереди.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiVoxPlayEvent(List<string> words) : EntityEventArgs
{
    public List<string> Words = words;
}
