using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Mood;

/// <summary>event_flags мудлета TG.</summary>
public enum ImperialMoodFlag : byte
{
    Fear,
    Art,
    Whimsy,
    Gaming,
    Food,
    Pain,
    Spiritual,
}

/// <summary>/datum/mood_event TG: текст лежит в локализации по ключу imperial-mood-{id}.</summary>
[Prototype]
public sealed partial class ImperialMoodEventPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>mood_change.</summary>
    [DataField]
    public int MoodChange;

    /// <summary>timeout в секундах, 0 — пока не снимут.</summary>
    [DataField]
    public float Timeout;

    /// <summary>hidden: не виден в списке и не влияет на показанное настроение.</summary>
    [DataField]
    public bool Hidden;

    [DataField]
    public List<ImperialMoodFlag> Flags = new();

    /// <summary>special_screen_obj: значок вместо обычной рожицы.</summary>
    [DataField]
    public string? Icon;

    public string DescriptionLoc => $"imperial-mood-{ID}";
}

/// <summary>
/// add_mood_event: другие системы поднимают его на существе, не завися от кода настроения.
/// Category — ключ мудлета (у одной категории одновременно один мудлет).
/// </summary>
[ByRefEvent]
public record struct ImperialMoodAddEvent(string Category, ProtoId<ImperialMoodEventPrototype> Event, EntityUid? Source = null);

/// <summary>clear_mood_event.</summary>
[ByRefEvent]
public record struct ImperialMoodClearEvent(string Category);

/// <summary>Клик по значку настроения: print_mood.</summary>
public sealed partial class ImperialMoodAlertEvent : BaseAlertEvent;
