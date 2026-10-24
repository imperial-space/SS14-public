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

/// <summary>/datum/language_holder: какие языки знает существо и на каком говорит.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialLanguageSpeakerComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<ImperialLanguagePrototype>> Spoken = new();

    [DataField, AutoNetworkedField]
    public List<ProtoId<ImperialLanguagePrototype>> Understood = new();

    /// <summary>grant_partial_language: язык понимается на столько процентов.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<ImperialLanguagePrototype>, int> Partial = new();

    /// <summary>add_blocked_language: язык нельзя ни понять, ни выучить, пока блок не снят.</summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<ImperialLanguagePrototype>> Blocked = new();

    /// <summary>selected_language.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ImperialLanguagePrototype>? Current;

    public EntityUid? MenuAction;
}

/// <summary>Открыть меню языков (Language Menu).</summary>
public sealed partial class ImperialLanguageMenuActionEvent : InstantActionEvent;

/// <summary>Клиент выбирает язык по умолчанию.</summary>
[Serializable, NetSerializable]
public sealed class ImperialLanguageSelectMessage(ProtoId<ImperialLanguagePrototype> language) : EntityEventArgs
{
    public readonly ProtoId<ImperialLanguagePrototype> Language = language;
}

/// <summary>Сервер просит клиента открыть меню языков.</summary>
[Serializable, NetSerializable]
public sealed class ImperialLanguageMenuOpenMessage : EntityEventArgs;

public static class ImperialLanguages
{
    public static readonly ProtoId<ImperialLanguagePrototype> Common = "Common";
}

public sealed class SharedImperialLanguageSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public bool CanSpeak(Entity<ImperialLanguageSpeakerComponent?> ent, ProtoId<ImperialLanguagePrototype> language)
    {
        // Без языкового компонента существо говорит на общем (животные, призраки, машины без мозгов).
        if (!Resolve(ent, ref ent.Comp, false))
            return language == ImperialLanguages.Common;

        return ent.Comp.Spoken.Contains(language) && !ent.Comp.Blocked.Contains(language);
    }

    /// <summary>Сколько процентов речи на языке понимает существо: 100 — полностью.</summary>
    public int Understanding(Entity<ImperialLanguageSpeakerComponent?> ent, ProtoId<ImperialLanguagePrototype> language)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return language == ImperialLanguages.Common ? 100 : 0;

        if (ent.Comp.Blocked.Contains(language))
            return 0;

        if (ent.Comp.Understood.Contains(language))
            return 100;

        var best = ent.Comp.Partial.GetValueOrDefault(language);
        foreach (var known in ent.Comp.Understood)
        {
            if (_proto.TryIndex(known, out var proto) && proto.Mutual.TryGetValue(language, out var mutual))
                best = Math.Max(best, mutual);
        }

        return Math.Clamp(best, 0, 100);
    }

    /// <summary>get_selected_language: выбранный, иначе самый приоритетный из доступных.</summary>
    public ProtoId<ImperialLanguagePrototype> GetCurrent(Entity<ImperialLanguageSpeakerComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return ImperialLanguages.Common;

        if (ent.Comp.Current is { } current && CanSpeak(ent, current))
            return current;

        ProtoId<ImperialLanguagePrototype>? best = null;
        var priority = int.MinValue;
        foreach (var spoken in ent.Comp.Spoken)
        {
            if (ent.Comp.Blocked.Contains(spoken) || !_proto.TryIndex(spoken, out var proto) || proto.Priority <= priority)
                continue;

            best = spoken;
            priority = proto.Priority;
        }

        return best ?? ImperialLanguages.Common;
    }
}

/// <summary>Набор языков по виду или типу существа (language_holder TG). Читается и клиентом, поэтому в общей сборке.</summary>
[Prototype]
public sealed partial class ImperialLanguageSetPrototype : IPrototype
{
    /// <summary>Id вида (Human, Reptilian…) либо Borg / StationAi / Default.</summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ProtoId<ImperialLanguagePrototype>> Spoken = new();

    [DataField]
    public List<ProtoId<ImperialLanguagePrototype>> Understood = new();

    [DataField]
    public Dictionary<ProtoId<ImperialLanguagePrototype>, int> Partial = new();
}
