using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Mood;

/// <summary>clear_mood_event.</summary>
[ByRefEvent]
public record struct ImperialMoodClearEvent(string Category);
