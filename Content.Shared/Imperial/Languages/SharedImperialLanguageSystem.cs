using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Languages;

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
