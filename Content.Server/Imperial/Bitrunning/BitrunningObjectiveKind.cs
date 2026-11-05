using System.Linq;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server.Imperial.Bitrunning;

public enum BitrunningObjectiveKind : byte
{
    /// <summary>Добыть зашифрованный тайник (найти, убить стража или набрать очки).</summary>
    Cache,
    /// <summary>Доставить тайник на точку отправки в убежище.</summary>
    Deliver,
    /// <summary>Доставить диковинки (secondary objectives).</summary>
    Curiosity,
}
