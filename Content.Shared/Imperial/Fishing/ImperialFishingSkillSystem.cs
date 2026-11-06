using Content.Shared.Mind;
using Content.Shared.Popups;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Навык рыбалки (/datum/skill/fishing): опыт на разуме, уровни 1..7 и открываемые ими умения.
/// </summary>
public sealed class ImperialFishingSkillSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>get_skill_level: 1 (нет навыка) .. 7 (легенда).</summary>
    public int GetLevel(EntityUid user)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _) || !TryComp<ImperialFishingSkillComponent>(mindId, out var skill))
            return ImperialFishing.SkillLevelNone;

        return LevelFromExperience(skill.Experience);
    }

    public static int LevelFromExperience(float experience)
    {
        var level = 1;
        for (var i = 0; i < ImperialFishing.SkillExperience.Length; i++)
        {
            if (experience >= ImperialFishing.SkillExperience[i])
                level = i + 1;
        }

        return level;
    }

    /// <summary>get_skill_modifier(SKILL_VALUE_MODIFIER).</summary>
    public float GetValueModifier(EntityUid user)
    {
        return ImperialFishing.SkillValueModifier[GetLevel(user) - 1];
    }

    /// <summary>adjust_experience с сообщением о новом уровне.</summary>
    public void AddExperience(EntityUid user, float amount)
    {
        if (amount == 0 || !_mind.TryGetMind(user, out var mindId, out _))
            return;

        var skill = EnsureComp<ImperialFishingSkillComponent>(mindId);
        var oldLevel = LevelFromExperience(skill.Experience);
        skill.Experience = Math.Max(0, skill.Experience + amount);
        Dirty(mindId, skill);

        var newLevel = LevelFromExperience(skill.Experience);
        if (newLevel > oldLevel)
            _popup.PopupEntity(Loc.GetString($"imperial-fishing-skill-level-{newLevel}"), user, user, PopupType.Medium);
    }

    /// <summary>TRAIT_EXAMINE_FISH (новичок): размер и вес рыбы.</summary>
    public bool CanExamineFish(EntityUid user) => GetLevel(user) >= ImperialFishing.SkillLevelNovice;

    /// <summary>TRAIT_EXAMINE_DEEPER_FISH (ученик): здоровье и голод рыбы, проценты удочки.</summary>
    public bool CanExamineDeeper(EntityUid user) => GetLevel(user) >= ImperialFishing.SkillLevelApprentice;

    /// <summary>TRAIT_EXAMINE_FISHING_SPOT (подмастерье): список рыбы в месте ловли.</summary>
    public bool CanExamineSpot(EntityUid user) => GetLevel(user) >= ImperialFishing.SkillLevelJourneyman;

    /// <summary>TRAIT_REVEAL_FISH (мастер): видно, что клюёт.</summary>
    public bool CanRevealFish(EntityUid user) => GetLevel(user) >= ImperialFishing.SkillLevelMaster;
}
