using System.Text;
using System.Text.RegularExpressions;
using Content.Shared.Speech;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>
/// Поросячья латынь (мутация piglatin из SS13): начальные согласные слова переносятся в конец, добавляется «ай».
/// </summary>
[RegisterComponent]
public sealed partial class PigLatinAccentComponent : Component;

public sealed class PigLatinAccentSystem : EntitySystem
{
    private static readonly Regex Words = new(@"[\p{L}]+", RegexOptions.Compiled);
    private const string Vowels = "аеёиоуыэюяaeiouyАЕЁИОУЫЭЮЯAEIOUY";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PigLatinAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<PigLatinAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Words.Replace(args.Message, match => Convert(match.Value));
    }

    private static string Convert(string word)
    {
        var split = 0;
        while (split < word.Length && Vowels.IndexOf(word[split]) < 0)
        {
            split++;
        }

        if (split == word.Length)
            return word;

        var upper = char.IsUpper(word[0]);
        var result = new StringBuilder(word.Length + 3);
        result.Append(word, split, word.Length - split);
        result.Append(word, 0, split);
        result.Append(char.IsLetter(word[^1]) && word[^1] < 'z' + 1 ? "ay" : "ай");

        var text = result.ToString().ToLowerInvariant();
        return upper ? char.ToUpperInvariant(text[0]) + text[1..] : text;
    }
}
