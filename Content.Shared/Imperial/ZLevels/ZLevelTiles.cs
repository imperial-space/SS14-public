using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// Какие тайлы ведут себя как в SS13 между этажами.
/// Открытое пространство (/turf/open/openspace) — сквозь него видно нижний этаж и всё падает вниз.
/// Стеклянный пол (/turf/open/floor/glass) — прозрачный: этаж ниже видно, но падать некуда.
/// Решётки и прочие «космические» тайлы стройки (isSpace: lattice и т.п.) — тоже прозрачные: сквозь прутья
/// виден этаж ниже, а не космос; стоять на них можно (/obj/structure/lattice — BLOCK_Z_OUT_DOWN).
/// </summary>
public static class ZLevelTiles
{
    public const string OpenSpace = "FloorImperialOpenSpace";

    private static readonly HashSet<string> Transparent = new()
    {
        OpenSpace,
        "FloorGlass",
        "FloorRGlass",
    };

    public static bool IsOpenSpace(ITileDefinitionManager defs, Tile tile)
    {
        return !tile.IsEmpty && defs[tile.TypeId].ID == OpenSpace;
    }

    public static bool IsTransparent(ITileDefinitionManager defs, Tile tile)
    {
        if (tile.IsEmpty)
            return false;
        var def = defs[tile.TypeId];
        return Transparent.Contains(def.ID) || IsSpaceLike(def);
    }

    /// <summary>Тайл стройки, сквозь который видно то, что под ним (решётка): рисуется поверх вида вниз непрозрачно.</summary>
    public static bool IsSpaceLike(ITileDefinitionManager defs, Tile tile)
    {
        return !tile.IsEmpty && IsSpaceLike(defs[tile.TypeId]);
    }

    private static bool IsSpaceLike(ITileDefinition def)
    {
        return def is ContentTileDefinition { MapAtmosphere: true } && def.ID != OpenSpace;
    }
}
