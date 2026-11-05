using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Mood;

/// <summary>
/// add_mood_event: другие системы поднимают его на существе, не завися от кода настроения.
/// Category — ключ мудлета (у одной категории одновременно один мудлет).
/// </summary>
[ByRefEvent]
public record struct ImperialMoodAddEvent(string Category, ProtoId<ImperialMoodEventPrototype> Event, EntityUid? Source = null);
