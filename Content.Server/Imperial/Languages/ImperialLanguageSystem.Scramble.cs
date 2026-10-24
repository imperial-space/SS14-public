using Robust.Shared.Random;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Content.Shared.Imperial.Languages;

namespace Content.Server.Imperial.Languages;

public sealed partial class ImperialLanguageSystem
{
    private const int ScrambleCacheLen = 50;
    private const int SentenceCacheLen = 20;
    private static readonly string Codespeak = "Codespeak";

    private static readonly Regex Sentences = new(@"(.+?(?:[\.!\?]|$))", RegexOptions.Compiled);

    /// <summary>generate_code_phrase: напитки, имена и места вперемешку.</summary>
    private static readonly string[] CodeWords =
    [
        "Водка", "Текила", "Пиво", "Виски", "Ром", "Джин", "Абсент", "Отвёртка", "Кровавая Мэри", "Кофе", "Чай", "Гоночный болид",
        "Капитан", "Брюс", "Агата", "Иван", "Мартин", "Ричард", "Сара", "Джонни", "Матильда", "Эрих",
        "мостик", "техи", "медблок", "бриг", "карго", "кухня", "дормы", "эвакуация", "реактор", "морг",
        "красный", "синий", "зелёный", "жёлтый", "золотой", "серебряный", "небо", "буря", "рассвет", "закат",
        "кот", "пёс", "ворон", "змея", "медведь", "лиса", "сова", "ящерица", "дельфин", "тигр",
    ];

    private sealed class ScrambleCache
    {
        public readonly Dictionary<string, string> Words = new();
        public readonly LinkedList<string> WordOrder = new();
        public readonly Dictionary<string, Dictionary<int, string>> Sentences = new();
        public readonly LinkedList<string> SentenceOrder = new();
    }

    private readonly Dictionary<string, ScrambleCache> _caches = new();

    private ScrambleCache Cache(ImperialLanguagePrototype language)
    {
        if (!_caches.TryGetValue(language.ID, out var cache))
            _caches[language.ID] = cache = new ScrambleCache();
        return cache;
    }

    /// <summary>scramble_paragraph: каждое предложение перемешивается отдельно.</summary>
    public string ScrambleParagraph(ImperialLanguagePrototype language, string input, int understanding)
    {
        if (understanding >= 100)
            return input;

        var result = new List<string>();
        foreach (Match match in Sentences.Matches(input))
        {
            var sentence = match.Groups[1].Value.Trim();
            if (sentence.Length > 0)
                result.Add(language.ID == Codespeak ? ScrambleCodespeak(language, sentence) : ScrambleSentence(language, sentence, understanding));
        }

        return string.Join(" ", result);
    }

    /// <summary>scramble_sentence: часть слов взаимопонятных языков остаётся, остальные перемешиваются.</summary>
    private string ScrambleSentence(ImperialLanguagePrototype language, string input, int understanding)
    {
        var cache = Cache(language);
        if (cache.Sentences.TryGetValue(input, out var byKey) && byKey.TryGetValue(understanding, out var cached))
            return cached;

        var words = new List<string>();
        var translated = new List<bool>();
        foreach (var word in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var baseWord = StripPunctuation(word);
            if (understanding > 0 && _random.Prob(understanding / 100f))
            {
                words.Add(word);
                translated.Add(false);
                continue;
            }

            var scrambled = ScrambleWord(language, baseWord);
            words.Add(scrambled);
            translated.Add(scrambled != baseWord);
        }

        if (words.Count == 0)
            return input;

        var sentence = new StringBuilder(Capitalize(words[0]));
        for (var i = 1; i < words.Count; i++)
        {
            var word = words[i];
            if (!translated[i])
            {
                sentence.Append(' ').Append(word);
                continue;
            }

            if (translated[i - 1] || _random.Prob(language.BetweenWordSpaceChance / 100f))
            {
                sentence.Append(' ');
            }
            else if (_random.Prob(language.BetweenWordSentenceChance / 100f))
            {
                sentence.Append(". ");
                word = Capitalize(word);
            }

            sentence.Append(word);
        }

        if (translated[^1])
            sentence.Append(LastPunctuation(input));

        var result = sentence.ToString();
        if (!cache.Sentences.TryGetValue(input, out byKey))
        {
            cache.Sentences[input] = byKey = new Dictionary<int, string>();
            cache.SentenceOrder.AddLast(input);
            if (cache.SentenceOrder.Count > SentenceCacheLen)
            {
                cache.Sentences.Remove(cache.SentenceOrder.First!.Value);
                cache.SentenceOrder.RemoveFirst();
            }
        }

        byKey[understanding] = result;
        return result;
    }

    /// <summary>scramble_word: слово собирается из слогов языка примерно той же длины.</summary>
    private string ScrambleWord(ImperialLanguagePrototype language, string input)
    {
        if (input.Length == 0)
            return input;

        var cache = Cache(language);
        var lower = input.ToLowerInvariant();
        if (!cache.Words.TryGetValue(lower, out var word))
        {
            if (language.Syllables.Count == 0 || language.Syllables.All(g => g.Count == 0))
            {
                word = new string('*', input.Length);
            }
            else
            {
                var size = Math.Max(input.Length + _random.Next(language.AdditionalSyllableLow, language.AdditionalSyllableHigh + 1), 1);
                var addSpace = false;
                var addPeriod = false;
                var sb = new StringBuilder();
                while (sb.Length < size)
                {
                    if (addPeriod)
                        sb.Append(". ");
                    else if (addSpace)
                        sb.Append(' ');
                    else if (sb.Length > 0 && language.SpecialCharacters.Count > 0 && _random.Prob(0.01f))
                        sb.Append(_random.Pick(language.SpecialCharacters));

                    var group = _random.Pick(language.Syllables.Where(g => g.Count > 0).ToList());
                    var next = _random.Pick(group);
                    sb.Append(addPeriod ? Capitalize(next) : next);
                    addPeriod = _random.Prob(language.SentenceChance / 100f);
                    addSpace = _random.Prob(language.SpaceChance / 100f);
                }

                word = sb.ToString();
            }

            cache.Words[lower] = word;
            cache.WordOrder.AddLast(lower);
            if (cache.WordOrder.Count > ScrambleCacheLen)
            {
                cache.Words.Remove(cache.WordOrder.First!.Value);
                cache.WordOrder.RemoveFirst();
            }
        }

        return input.Length >= 2 && input.Any(char.IsLetter) && input.All(c => !char.IsLetter(c) || char.IsUpper(c)) ? word.ToUpperInvariant() : word;
    }

    /// <summary>codespeak scramble_sentence: набор кодовых слов не короче исходной фразы.</summary>
    private string ScrambleCodespeak(ImperialLanguagePrototype language, string input)
    {
        var cache = Cache(language);
        if (cache.Words.TryGetValue(input, out var cached))
            return cached;

        var words = new List<string>();
        while (string.Join(", ", words).Length < input.Length)
        {
            words.Add(_random.Pick(CodeWords));
        }

        var result = Capitalize(string.Join(", ", words)) + LastPunctuation(input);
        cache.Words[input] = result;
        cache.WordOrder.AddLast(input);
        if (cache.WordOrder.Count > ScrambleCacheLen)
        {
            cache.Words.Remove(cache.WordOrder.First!.Value);
            cache.WordOrder.RemoveFirst();
        }

        return result;
    }

    private static string StripPunctuation(string word) => word.Trim('.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '-', '…');

    private static string LastPunctuation(string input)
    {
        if (input.Length == 0)
            return ".";

        var last = input[^1];
        return last is '!' or '?' or '.' ? last.ToString() : ".";
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
