using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Клиент → сервер: «перейти» к источнику тревоги.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiJumpEvent(NetEntity target) : EntityEventArgs
{
    public NetEntity Target = target;
}
