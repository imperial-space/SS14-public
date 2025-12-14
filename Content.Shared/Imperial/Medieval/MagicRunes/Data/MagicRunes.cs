using Robust.Shared.Serialization;

//=========================================================================
// MagicRunes.cs
//=========================================================================
// Purpose: Defines magic rune types, symbols, and meanings
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Data;

[Serializable, NetSerializable]
public enum MagicRune : byte
{
    Kael  = 0,
    Ryn   = 1,
    Vel   = 2,
    Oth   = 3,
    Thar  = 4,
    Lun   = 5,
    Seth  = 6,
    Mira  = 7,
}

public static class MagicRuneData
{
    private static readonly Dictionary<MagicRune, string> RuneSymbols = new()
    {
        { MagicRune.Kael, "☥" },
        { MagicRune.Ryn, "☯" },
        { MagicRune.Vel, "⚶" },
        { MagicRune.Oth, "☊" },
        { MagicRune.Thar, "⚚" },
        { MagicRune.Lun, "⛧" },
        { MagicRune.Seth, "☡" },
        { MagicRune.Mira, "⚵" }
    };

    private static readonly Dictionary<MagicRune, string> RuneMeanings = new()
    {
        { MagicRune.Kael, "Огонь / разрушение" },
        { MagicRune.Ryn, "Вода / адаптация" },
        { MagicRune.Vel, "Тень / наведение" },
        { MagicRune.Oth, "Иллюзия / магия" },
        { MagicRune.Thar, "Пустота / ничто" },
        { MagicRune.Lun, "Тьма / звезда смерти" },
        { MagicRune.Seth, "Яд / проклятие" },
        { MagicRune.Mira, "Защита / барьер" }
    };

    public static string GetSymbol(MagicRune rune) =>
        RuneSymbols.GetValueOrDefault(rune, "?");

    public static string GetMeaning(MagicRune rune) =>
        RuneMeanings.GetValueOrDefault(rune, "???");
}

