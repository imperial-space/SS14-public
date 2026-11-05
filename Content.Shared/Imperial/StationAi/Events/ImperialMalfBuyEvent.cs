using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Клиент → сервер: купить модуль.</summary>
[Serializable, NetSerializable]
public sealed class ImperialMalfBuyEvent(string module) : EntityEventArgs
{
    public string Module = module;
}
