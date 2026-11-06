using Robust.Shared.Map;

namespace Content.Server.Imperial.Chaplain;

/// <summary>
/// Удар святой книгой по полу: проявить скрытое тёмными силами в радиусе. <see cref="Revealed"/> — сколько проявлено.
/// </summary>
[ByRefEvent]
public record struct ImperialHolyRevealEvent(MapCoordinates Center, float Range, int Revealed = 0);
