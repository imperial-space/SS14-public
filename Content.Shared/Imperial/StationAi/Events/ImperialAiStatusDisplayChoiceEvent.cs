using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Клиент → сервер: выбрана картинка дисплеев (null — «Нет»).</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiStatusDisplayChoiceEvent(string? state) : EntityEventArgs
{
    public string? State = state;
}
