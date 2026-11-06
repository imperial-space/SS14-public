using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Клиент → сервер: выбран пункт списка.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiListChoiceEvent(ImperialAiListKind kind, NetEntity entity) : EntityEventArgs
{
    public ImperialAiListKind Kind = kind;
    public NetEntity Entity = entity;
}
