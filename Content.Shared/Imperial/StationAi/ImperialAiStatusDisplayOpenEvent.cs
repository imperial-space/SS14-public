using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Сервер → клиент: окно выбора картинки с текущей.</summary>
[Serializable, NetSerializable]
public sealed class ImperialAiStatusDisplayOpenEvent(string? current) : EntityEventArgs
{
    public string? Current = current;
}
