using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Сервер → клиент: законы с номерами и доступные каналы.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiLawsEvent(List<string> laws, List<ImperialAiChannelEntry> channels) : EntityEventArgs
{
    public List<string> Laws = laws;
    public List<ImperialAiChannelEntry> Channels = channels;
}
