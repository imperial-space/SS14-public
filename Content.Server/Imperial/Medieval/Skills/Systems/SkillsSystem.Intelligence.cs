using System.Linq;
using System.Text;
using Content.Server._CP14.Workbench;
using Content.Server.Examine;
using Content.Server.Imperial.Medieval.Language;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Imperial.Medieval.Medical;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Speech;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem
{
    [Dependency] private readonly ExamineSystem _examine = default!;
    [Dependency] private readonly LanguageSystem _lang = default!;

    private void InitializeIntelligence()
    {
        SubscribeLocalEvent<SkillsComponent, GetHealingSpeedModifiersEvent>(OnGetHealingSpeedModifiers);
        SubscribeLocalEvent<SkillsComponent, CheckWorkbenchCraftSpeedModifiersEvent>(OnGetCraftingSpeedModifiers);
        SubscribeLocalEvent<SkillsComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<SkillsComponent, MapInitEvent>(OnCanUseMana);

        SubscribeNetworkEvent<GetEnteredChatTextResponseMessage>(OnGetMessage);
    }

    private void OnGetHealingSpeedModifiers(EntityUid uid, SkillsComponent comp, ref GetHealingSpeedModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, IntelligenceId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveHealingSpeedModifier"] : proto.Modifiers["NegativeHealingSpeedModifier"]) * diff;
    }

    private void OnGetCraftingSpeedModifiers(EntityUid uid, SkillsComponent comp, ref CheckWorkbenchCraftSpeedModifiersEvent args)
    {
        if (args.User != uid)
            return;

        var (proto, level) = GetSkill(uid, IntelligenceId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveConstructionSpeedModifier"] : proto.Modifiers["NegativeConstructionSpeedModifier"]) * diff;
    }

    private void IntelligenceLevelSet(EntityUid uid, int level, int oldLevel)
    {
        var (proto, _) = GetSkill(uid, IntelligenceId);

        var diff = level - oldLevel;

        if (TryComp<ManaComponent>(uid, out var mana))
        {
            mana.MaxMana += (level > 10 ? proto.Modifiers["PositiveManaModifier"] : proto.Modifiers["NegativeManaModifier"]) * diff;
            Dirty(uid, mana);
        }

        var skills = EnsureComp<SkillsComponent>(uid);
        if (skills.LanguagesGain)
            return;

        if (level <= 16)
            return;

        skills.LanguagesGain = true;
        var langs = _proto.EnumeratePrototypes<LanguagePrototype>().Where(x => x.HighIntelligenceAllowed && !_lang.CanSpeak(uid, x)).ToList();
        for (var i = 0; i < 2; i++)
        {
            if (langs.Count <= i)
                break;

            _lang.AddSpokenLanguage(uid, _random.PickAndTake(langs).ID, LanguageKnowledge.BadSpeak);
        }

        if (level < 20)
            return;

        foreach (var item in _proto.EnumeratePrototypes<LanguagePrototype>().Where(x => x.HighIntelligenceAllowed && !_lang.CanSpeak(uid, x)))
        {
            _lang.AddSpokenLanguage(uid, item.ID, LanguageKnowledge.Speak);
        }
    }

    private void OnCanUseMana(Entity<SkillsComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<ManaComponent>(ent) && IntelligenceMin(ent))
        {
            RemComp<ManaComponent>(ent);
        }
    }

    private void OnAccent(EntityUid uid, SkillsComponent component, AccentGetEvent args)
    {
        var (proto, level) = GetSkill(uid, IntelligenceId);

        if (level >= 5)
            return;

        var prob = proto.Modifiers["LowStupidityChance"];

        if (level <= 1)
            prob = 1;

        args.Message = Accentuate(args.Message, prob);
    }

    private void OnGetMessage(GetEnteredChatTextResponseMessage message)
    {
        _examine.SendExamineTooltip(GetEntity(message.User), GetEntity(message.Target), FormattedMessage.FromUnformatted(message.Text != string.Empty ? $"По глазам легко читается - '{message.Text}'." : $"Кажется, {Identity.Name(GetEntity(message.Target), EntityManager, GetEntity(message.User))} не планирует ничего говорить."), false, false);
    }

    private string Accentuate(string message, float scale)
    {
        var builder = new StringBuilder();

        var wordBeginIndex = 0;

        for (var i = 0; i < message.Length; i++)
        {
            var ch = char.ToLower(message[i]);
            // A word ends when one of the following is found: a space, a sentence end, or EOM
            if (char.IsWhiteSpace(ch) || (ch is '.' or '!' or '?' or '~' or '-' or ',') || i == message.Length - 1)
            {
                var wordLength = i - wordBeginIndex + 1;
                if (wordLength > 0)
                {
                    if (!_random.Prob(scale))
                    {
                        builder.Append(message.Substring(wordBeginIndex, wordLength));
                        continue;
                    }

                    var replacement = _random.Pick(new[]
                    {
                        "ээ",
                        "э-э-э",
                        "эмм",
                        "уэээ",
                        "ааа...",
                        "ммм",
                        "эмм",
                        "ыээ"
                    });

                    if (wordBeginIndex == 0)
                    {
                        var replacementBuilder = new StringBuilder(replacement);
                        replacementBuilder[0] = char.ToUpper(replacement[0]);
                        replacement = replacementBuilder.ToString();
                    }

                    builder.Append(replacement);
                }

                if (char.IsWhiteSpace(ch) || (ch is '.' or '!' or '?' or '~' or '-' or ','))
                    builder.Append(ch);
                if (ch is ('.' or '!' or '?' or '~' or ',') && message.Length >= i + 2 && char.ToLower(message[i + 1]) is not ('.' or '!' or '?' or '~' or ','))
                    builder.Append(' ');

                wordBeginIndex = i + 1;
            }
        }

        return builder.ToString();
    }
}
