using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Клиент → сервер: следить за ботом или вызвать его к «глазу».</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiBotCommandEvent(NetEntity bot, ImperialAiBotCommand command) : EntityEventArgs
{
    public NetEntity Bot = bot;
    public ImperialAiBotCommand Command = command;
}
