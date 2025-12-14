using Content.Shared.Implants.Components;
using Robust.Shared.Containers;
using Content.Shared.Implants;

namespace Content.Shared.Imperial.Medieval.Language;

public abstract partial class SharedLanguageSystem
{
    private void InitializeImplants()
    {
        SubscribeLocalEvent<TranslatorImplantComponent, GetLanguagesEvent>(OnGetLanguages);
        SubscribeLocalEvent<TranslatorImplantComponent, ImplantImplantedEvent>(OnImplanted);
        SubscribeLocalEvent<TranslatorImplantComponent, ImplantRemovedEvent>(OnUnimplanted);
        SubscribeLocalEvent<TranslatorImplantComponent, ContainerGettingRemovedAttemptEvent>(OnRemoveAttempt);
    }

    private void OnGetLanguages(EntityUid uid, TranslatorImplantComponent component, ref GetLanguagesEvent args)
    {
        foreach (var (key, value) in component.Languages)
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

    private void OnImplanted(EntityUid uid, TranslatorImplantComponent comp, ref ImplantImplantedEvent args)
    {
        UpdateUi(args.Implanted);
        comp.ImplantedEntity = args.Implanted;
    }

    private void OnUnimplanted(EntityUid uid, TranslatorImplantComponent comp, ref ImplantRemovedEvent args)
    {
        UpdateUi(args.Implanted);
        comp.ImplantedEntity = null;
    }

    private void OnRemoveAttempt(EntityUid uid, TranslatorImplantComponent component, ContainerGettingRemovedAttemptEvent args)
    {
        if (component.Permanent && component.ImplantedEntity != null)
            args.Cancel();
    }
}
