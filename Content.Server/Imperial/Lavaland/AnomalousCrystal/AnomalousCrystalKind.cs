using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.AnomalousCrystal;

public enum AnomalousCrystalKind : byte
{
    /// <summary>Воскрешает мёртвых рядом клоунами.</summary>
    Honk,
    /// <summary>Перекрашивает окрестности в случайную тему.</summary>
    ThemeWarp,
    /// <summary>Выпускает смертельный болт колосса.</summary>
    Emitter,
    /// <summary>Воскрешает мёртвых рядом, но их больше не спасти иначе.</summary>
    DarkReprise,
    /// <summary>Позволяет призракам становиться лайтгейстами.</summary>
    Helpers,
    /// <summary>Позволяет вселиться в мелкое животное рядом.</summary>
    Possessor,
}
