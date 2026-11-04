using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public enum ImperialFishingPortalVisuals : byte
{
    /// <summary>Состояние наложения активного портала (пустая строка — выключен).</summary>
    Overlay,
}
