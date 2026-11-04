using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Languages;

/// <summary>Сервер просит клиента открыть меню языков.</summary>
[Serializable, NetSerializable]
public sealed class ImperialLanguageMenuOpenMessage : EntityEventArgs;
