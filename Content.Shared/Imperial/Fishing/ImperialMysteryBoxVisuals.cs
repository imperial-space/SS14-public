using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public enum ImperialMysteryBoxVisuals : byte
{
    Open,

    /// <summary>Прототип показываемого приза (пустая строка — нет).</summary>
    Prize,

    /// <summary>Приз можно взять (жёлтая подсветка).</summary>
    Claimable,
}
