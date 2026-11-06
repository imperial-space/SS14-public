using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Окно открыто или закрыто: подписаться на состояние или отписаться. Сервер проверяет права.</summary>
[Serializable, NetSerializable]
public sealed class CadiaPanelSubscribeEvent(bool subscribe) : EntityEventArgs
{
    public readonly bool Subscribe = subscribe;
}
