using System.Linq;
using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Content.Shared.Imperial.Medieval.Skills;

//=========================================================================
// MagicRuneSystem.Helpers.cs
//=========================================================================
// Purpose: Helper methods for rune initialization, learning, and power calculation
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Systems;

public partial class MagicRuneSystem
{
    public void InitializeScroll(EntityUid uid, MagicScrollComponent scroll)
    {
        scroll.EncryptedRunes.Clear();

        var allRunes = Enum.GetValues<MagicRune>().ToList();
        _random.Shuffle(allRunes);

        var runeCount = scroll.MaxRunes;
        runeCount = Math.Min(runeCount, allRunes.Count);

        scroll.EncryptedRunes.AddRange(allRunes.Take(runeCount));

        RecalculateScrollPower(uid, scroll);

        Dirty(uid, scroll);
    }

    private void RecalculateScrollPower(EntityUid uid, MagicScrollComponent scroll)
    {
        if (scroll.Bad)
        {
            scroll.Power = scroll.BasicPower;
            return;
        }

        scroll.Power = scroll.BasicPower + scroll.DecodedRunes.Count * scroll.PointsPerDecodedRune;
        Dirty(uid, scroll);
    }

    public void PopulateStartRunes(EntityUid uid, MagicRuneKnowledgeComponent comp, int intelligence)
    {
        PopulateRandomRunes(uid, comp, 1);
    }

    public void PopulateRandomRunes(EntityUid uid, MagicRuneKnowledgeComponent comp, int count)
    {
        var unknownRunes = Enum.GetValues<MagicRune>()
            .Except(comp.KnownRunes)
            .ToList();

        _random.Shuffle(unknownRunes);

        foreach (var rune in unknownRunes.Take(count))
        {
            if (comp.KnownRunes.Count >= comp.MaxRunesKnowledge)
                break;

            comp.KnownRunes.Add(rune);
        }

        Dirty(uid, comp);
    }

    private void HandleRuneLearning(EntityUid user, EntityUid stone, MagicRune rune)
    {
        if (!TryComp<MagicRuneKnowledgeComponent>(user, out var knowledge))
        {
            _popupSystem.PopupPredicted("Я слишком туп для такого..", user, user);
            return;
        }

        if (knowledge.KnownRunes.Contains(rune))
        {
            _popupSystem.PopupPredicted("Я уже знаю эту руну..", user, user);
            return;
        }

        if (knowledge.KnownRunes.Count >= knowledge.MaxRunesKnowledge)
        {
            _popupSystem.PopupPredicted("Я знаю максимальное количество рун..", user, user);
            return;
        }

        if (!PopulateRune(user, knowledge, rune))
            return;

        _popupSystem.PopupPredicted($"Вы изучили новую руну - {rune}!", user, user);

        if (_net.IsServer)
        {
            QueueDel(stone);
        }
    }

    public bool PopulateRune(EntityUid uid, MagicRuneKnowledgeComponent comp, MagicRune rune)
    {
        if (comp.KnownRunes.Count >= comp.MaxRunesKnowledge)
            return false;

        if (comp.KnownRunes.Contains(rune))
            return false;

        if (!comp.KnownRunes.Add(rune))
            return false;

        Dirty(uid, comp);
        return true;
    }

    public int CalculateIntegrityGiven(EntityUid target)
    {
        if (!TryComp<MagicRuneKnowledgeComponent>(target, out var comp))
            return 0;

        const int basePoints = 10;
        var bonus = comp.KnownRunes.Count * 6;

        return basePoints + bonus;
    }

    private int GetIntelligence(EntityUid target)
    {
        return GetSkill(target, "Intelligence").Item2;
    }

    private (SkillPrototype, int) GetSkill(EntityUid uid, string id)
    {
        var proto = _prototype.Index<SkillPrototype>(id);

        if (!TryComp<SkillsComponent>(uid, out var skillComponent))
            return (proto, 10);

        return (proto, skillComponent.Levels.GetValueOrDefault(id, 10));
    }
}
