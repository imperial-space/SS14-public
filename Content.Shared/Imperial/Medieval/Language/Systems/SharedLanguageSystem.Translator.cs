using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Toggleable;

namespace Content.Shared.Imperial.Medieval.Language;

public abstract partial class SharedLanguageSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private void InitializeTranslator()
    {
        SubscribeLocalEvent<HandheldTranslatorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<HandheldTranslatorComponent, GetLanguagesEvent>(OnTranslatorGetLanguages);
        SubscribeLocalEvent<HandsComponent, GetLanguagesEvent>(OnGetLanguages);
    }

    private void OnGetLanguages(EntityUid uid, HandsComponent comp, ref GetLanguagesEvent args)
    {
        foreach (var item in _hands.EnumerateHeld(uid))
        {
            RaiseLocalEvent(item, ref args);
        }
    }

    private void OnTranslatorGetLanguages(EntityUid uid, HandheldTranslatorComponent comp, ref GetLanguagesEvent args)
    {
        if (!comp.Enabled)
            return;
        if (!TryComp<LanguageSpeakerComponent>(comp.User, out var speaker))
            return;
        if (speaker.Languages.Keys.Where(x => comp.Languages.ContainsKey(x)).Count() <= 0)
            return;

        foreach (var (key, value) in comp.Languages)
        {
            if (args.Translator.ContainsKey(key))
            {
                if (args.Translator[key] >= value)
                    continue;
                args.Translator[key] = value;
            }
            else
                args.Translator.Add(key, value);
        }
    }

    private void OnExamined(EntityUid uid, HandheldTranslatorComponent component, ExaminedEvent args)
    {
        var state = Loc.GetString(component.Enabled
            ? "translator-enabled"
            : "translator-disabled");

        args.PushMarkup(state);
    }

    protected void OnAppearanceChange(EntityUid translator, HandheldTranslatorComponent? comp = null)
    {
        if (comp == null && !TryComp(translator, out comp))
            return;

        _appearance.SetData(translator, ToggleableVisuals.Enabled, comp.Enabled);
    }
}
