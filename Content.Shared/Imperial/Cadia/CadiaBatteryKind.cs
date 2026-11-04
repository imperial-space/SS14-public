using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

// ═══════════════════════ Орбитальные батареи ═══════════════════════

[Serializable, NetSerializable]
public enum CadiaBatteryKind : byte
{
    /// <summary>Макропушки: залп из нескольких снарядов с разбросом, нужны снаряды.</summary>
    Macro,

    /// <summary>Лэнс: один точный сверхмощный луч, долгая перезарядка, без снарядов.</summary>
    Lance,
}
