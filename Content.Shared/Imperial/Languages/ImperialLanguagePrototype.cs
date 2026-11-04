using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Languages;

/// <summary>/datum/language из TG: как язык звучит для тех, кто его не знает.</summary>
[Prototype]
public sealed partial class ImperialLanguagePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>Префикс «,ключ» в начале фразы выбирает язык.</summary>
    [DataField(required: true)]
    public string Key = string.Empty;

    /// <summary>default_priority: чем выше, тем раньше язык выбирается по умолчанию.</summary>
    [DataField]
    public int Priority;

    [DataField]
    public int SentenceChance = 2;

    [DataField]
    public int BetweenWordSentenceChance;

    [DataField]
    public int SpaceChance = 20;

    [DataField]
    public int BetweenWordSpaceChance = 100;

    [DataField]
    public int AdditionalSyllableLow = -1;

    [DataField]
    public int AdditionalSyllableHigh = 3;

    /// <summary>Группы слогов: сначала равновероятно выбирается группа, затем слог в ней.</summary>
    [DataField]
    public List<List<string>> Syllables = new();

    [DataField]
    public List<string> SpecialCharacters = new();

    /// <summary>mutual_understanding: знающие этот язык понимают указанные языки на столько процентов.</summary>
    [DataField]
    public Dictionary<ProtoId<ImperialLanguagePrototype>, int> Mutual = new();

    /// <summary>NO_STUTTER: речь на языке не заикается.</summary>
    [DataField]
    public bool NoStutter;

    /// <summary>TONGUELESS_SPEECH.</summary>
    [DataField]
    public bool Tongueless;

    /// <summary>SPAN_ROBOT: синтетический голос.</summary>
    [DataField]
    public bool Robot;

    /// <summary>LANGUAGE_HIDE_ICON_IF_UNDERSTOOD: понимающим не показывается метка языка.</summary>
    [DataField]
    public bool HideIconIfUnderstood;

    [DataField]
    public Color Color = Color.FromHex("#a0a0a0");
}
