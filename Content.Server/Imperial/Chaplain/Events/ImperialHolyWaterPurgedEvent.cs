using Content.Shared.Imperial.Antimagic;

namespace Content.Server.Imperial.Chaplain;

/// <summary>
/// Святая вода очистила сущность и выходит из тела.
/// </summary>
[ByRefEvent]
public readonly record struct ImperialHolyWaterPurgedEvent(Entity<ImperialHolyWaterComponent> Holy);
