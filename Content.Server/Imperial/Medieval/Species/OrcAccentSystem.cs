using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Speech;

namespace Content.Server.Speech.EntitySystems;

public sealed class OrcAccentSystem : EntitySystem
{
    // Окончания, суффиксы и другие части слов для замены
    // Увы, без NLP дальше не уйти!
    // Причастия/деепричастия обрабатываться не должны.
    private static readonly (string ending, string replacement)[] VerbEndings =
    {
        // Настоящее + будущее совершенное время
        ("аю", "ать"), ("аешь", "ать"), ("аёшь", "ать"), ("ает", "ать"),
        ("аёт", "ать"), ("аем", "ать"), ("аём", "ать"), ("ают", "ать"),

        ("ят", "еть"), ("ит", "еть"), ("ишь", "еть"),

        ("лю", "ить"), ("ью", "ить"), ("ьешь", "ить"), ("ьет", "ить"),
        ("ьем", "ить"), ("ьют", "ить"), ("иву", "ить"), ("ивешь", "ить"),
        ("ивет", "ить"), ("ивем", "ить"), ("ивут", "ить"),

        // Будущее время
        ("ам", "ать"), ("ашь", "ать"), ("ану", "ать"), ("аст", "ать"),
        ("адим", "ать"),

        // Прошедшее время
        ("ал", "ать"), ("ала", "ать"), ("ало", "ать"), ("али", "ать"),
        ("ил", "ить"), ("ила", "ить"), ("ило", "ить"), ("или", "ить"),
        ("ел", "еть"), ("ели", "еть"), ("ело", "еть"), ("ела", "еть"),
        ("ул", "уть"), ("ула", "уть"), ("ули", "уть"), ("уло", "уть"),
        ("ыл", "ыть"), ("ыла", "ыть"), ("ыло", "ыть"), ("ыли", "ыть"),
        ("ял", "ять"), ("яла", "ять"), ("яло", "ять"), ("яли", "ять"),
        ("ла", "ти"), ("ло", "ти"), ("ли", "ти"),

        // Повелительное наклонение (исключения)
        ("ай", "ать"), ("иви", "ить")
    };
    // Замены в повелительных глаголах проверяется отдельно:
    // Орки – Оркать (неправильно, не обрабатывается);
    // БегиТЕ - Бегать (правильно, обрабатывается).
    private static readonly (string ending, string replacement)[] VerbEndingsImperative =
    {
        ("иви", "ить"), ("иве", "ить"), ("йди", "йти"), ("ли", "лить"),
        ("ади", "ать"), ("ди", "дти"), ("ей", "ить"), ("ае", "ать"),
        ("аё", "ать"), ("ье", "ить"), ("ьё", "ить"), ("ай", "ать"),
        ("и", "ать"), ("ь", "ить"),
    };
    private static readonly Regex RegexWordSplit = new(@"(?<=[^\p{L}\d])|(?=[^\p{L}\d])");
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrcAccentComponent, AccentGetEvent>(OnAccent);
    }

    private string MatchCase(string src, string dest)
    {
        if (string.IsNullOrEmpty(src)) return dest;
        // Целевое окончание может иметь только два регистра;
        // Проверяем его по последней букве корня.
        if (char.IsUpper(src[^1])) return dest.ToUpper();
        return dest;
    }

    public string ToInfinitive(string word)
    {
        var lower = word.ToLower();

        // Удаление постфиксов (повелительных и возвратных) если они есть:
        // Бьется - Бьет;
        // Рубитесь - Руби.
        bool reflexive = lower.EndsWith("ся") || lower.EndsWith("сь");
        if (reflexive) lower = lower.Substring(0, lower.Length - 2);
        bool imperative = lower.EndsWith("те");

        // Повелительное наклонение – отдельная проверка...
        if (imperative)
        {
            lower = lower.Substring(0, lower.Length - 2);
            foreach (var (ending, replacement) in VerbEndingsImperative)
            {
                if (lower.EndsWith(ending))
                {
                    int end_index = lower.Length - ending.Length;

                    if (end_index == 0) return word;

                    string stem = word.Substring(0, end_index);
                    string infinitive = reflexive ? stem + replacement + "ся" : stem + replacement;
                    return MatchCase(word, infinitive);
                }
            }
            return word;
        }

        // Последовательная проверка на окончания вместо наивных Regex замен
        foreach (var (ending, replacement) in VerbEndings)
        {
            if (lower.EndsWith(ending))
            {
                int end_index = lower.Length - ending.Length;

                if (end_index == 0) return word;

                string stem = word.Substring(0, end_index);
                // TODO: Проверить корень и заменить исключения (i.e. сарай, аколит).
                string infinitive = reflexive ? stem + replacement + "ся" : stem + replacement;
                return MatchCase(word, infinitive);
            }
        }
        return word;
    }

    public string Accentuate(string message, OrcAccentComponent component, string? name)
    {
        // Прямые замены слов и местоимений через акцент замены
        var msg = _replacement.ApplyReplacements(message, "orc");

        var result = new StringBuilder();

        // Каждое слово обрабатывается отдельно и единожды
        foreach (var element in RegexWordSplit.Split(msg))
        {
            // Замена "Я" с учетом регистра
            if (element.ToLower() == "я")
            {
                var pronoun = name == null ? "моя" : name;
                result.Append(MatchCase(element, pronoun.Substring(0, 1))).Append(pronoun.Substring(1));
                continue;
            }

            // Приведение глаголов к неопределённой форме
            result.Append(element.Length <= 1 ? element : ToInfinitive(element));
        }

        return result.ToString();
    }

    private void OnAccent(EntityUid uid, OrcAccentComponent component, AccentGetEvent args)
    {
        // При разговоре на орочьем языке акцент не применяется
        if (TryComp<LanguageSpeakerComponent>(uid, out var comp))
        {
            if (comp.CurrentLanguage == "Orc")
            {
                return;
            }
        }
        // Достаточно глупые орки вместо "я" используют свое имя
        var firstname = Name(uid).Split(' ')[0];
        args.Message = Accentuate(args.Message, component, _skills.CanRead(uid) ? null : firstname);
    }
}
