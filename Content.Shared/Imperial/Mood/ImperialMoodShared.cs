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

/// <summary>
/// /datum/personality TG. Выбирается чертой с тем же id в категории ImperialPersonalities;
/// личности из одной группы несовместимы.
/// </summary>
[Prototype]
public sealed partial class ImperialPersonalityPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>savefile_key TG: по нему системы узнают личность.</summary>
    [DataField(required: true)]
    public string Key = string.Empty;

    [DataField]
    public List<string> Groups = new();
}

/// <summary>/datum/quirk/depression: иногда без причины грустно.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkDepressionComponent : Component;

/// <summary>/datum/quirk/jolly: иногда без причины радостно.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkJollyComponent : Component;

/// <summary>/datum/quirk/hypersensitive: mood_modifier +0.5.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkHypersensitiveComponent : Component;

/// <summary>/datum/quirk/unstable (TRAIT_UNSTABLE): рассудок не восстанавливается.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkUnstableComponent : Component;

/// <summary>/datum/quirk/item_quirk/family_heirloom.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkFamilyHeirloomComponent : Component
{
    [DataField]
    public List<EntProtoId> Heirlooms = new();

    public EntityUid? Item;
    public bool Given;
}

/// <summary>/datum/quirk/all_nighter: «копилка сна» и усталость без стимуляторов.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkAllNighterComponent : Component
{
    [DataField]
    public List<string> Stimulants = new();

    public float SleepBank;
}

/// <summary>/datum/quirk/badback: рюкзак на спине — боль.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkBadBackComponent : Component;

/// <summary>/datum/quirk/erratic: личность то и дело меняется.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkErraticComponent : Component
{
    public List<string> BasePersonalities = new();
    public TimeSpan NextChange;
    public int Index;
    public bool Initialized;
}

/// <summary>Семейная реликвия: чья она.</summary>
[RegisterComponent]
public sealed partial class ImperialHeirloomComponent : Component
{
    public EntityUid Owner;
    public string Family = string.Empty;
}
