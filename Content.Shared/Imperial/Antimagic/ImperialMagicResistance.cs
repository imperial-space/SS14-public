using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Antimagic;

/// <summary>
/// Вид магии и защиты от неё (MAGIC_RESISTANCE / MAGIC_RESISTANCE_HOLY из SS13).
/// </summary>
[Flags]
public enum ImperialMagicResistance : byte
{
    None = 0,

    /// <summary>Обычная магия: её блокирует антимагия нулевого стержня.</summary>
    Magic = 1 << 0,

    /// <summary>Нечестивая магия культа: её блокирует ещё и святая вода в теле.</summary>
    Holy = 1 << 1,

    All = Magic | Holy,
}
