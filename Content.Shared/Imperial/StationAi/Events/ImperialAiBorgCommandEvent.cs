using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Клиент → сервер: следить за киборгом или войти в оболочку.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiBorgCommandEvent(NetEntity borg, ImperialAiBorgCommand command) : EntityEventArgs
{
    public NetEntity Borg = borg;
    public ImperialAiBorgCommand Command = command;
}
