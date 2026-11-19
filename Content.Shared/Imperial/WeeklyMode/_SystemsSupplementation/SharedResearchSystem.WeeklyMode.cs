using System.Linq;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Research.Systems;

public abstract partial class SharedResearchSystem
{
    public List<WeeklyTechnologyData> GetAvailableWeeklyTechnologies(EntityUid uid, TechnologyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component, false) || !component.WeeklyModeOnly)
            return new List<WeeklyTechnologyData>();

        var availableTechnologies = new List<WeeklyTechnologyData>();
        var disciplineTiers = GetDisciplineTiers(component);
        foreach (var tech in component.WeeklyTechnologies)
        {
            if (IsWeeklyTechnologyAvailable(component, tech, disciplineTiers))
                availableTechnologies.Add(tech);
        }

        return availableTechnologies;
    }

    public bool IsWeeklyTechnologyAvailable(TechnologyDatabaseComponent component, WeeklyTechnologyData tech, Dictionary<string, int>? disciplineTiers = null)
    {
        disciplineTiers ??= GetDisciplineTiers(component);

        if (!component.SupportedDisciplines.Contains(tech.Branch))
            return false;

        if (!disciplineTiers.TryGetValue(tech.Branch, out var tier) || tech.Tier > tier)
            return false;

        if (component.WeeklyUnlockedTechnologies.Contains(tech.TechnologyId))
            return false;

        return true;
    }

    private int GetHighestWeeklyDisciplineTier(TechnologyDatabaseComponent component, TechDisciplinePrototype techDiscipline)
    {
        var allTech = component.WeeklyTechnologies
            .Where(p => p.Branch == techDiscipline.ID)
            .ToList();
        var unlockedTech = allTech
            .Where(p => component.WeeklyUnlockedTechnologies.Contains(p.TechnologyId))
            .ToList();

        var highestTier = techDiscipline.TierPrerequisites.Keys.Max();
        var tier = 2; //tier 1 is always given

        while (tier <= highestTier)
        {
            var unlockedTierTech = unlockedTech.Where(p => p.Tier == tier - 1).ToList();
            var allTierTech = allTech.Where(p => p.Tier == tier - 1).ToList();

            if (allTierTech.Count == 0)
                break;

            var percent = (float) unlockedTierTech.Count / allTierTech.Count;
            if (percent < techDiscipline.TierPrerequisites[tier])
                break;

            if (tier >= techDiscipline.LockoutTier &&
                component.MainDiscipline != null &&
                techDiscipline.ID != component.MainDiscipline)
                break;
            tier++;
        }

        return tier - 1;
    }

    private IEnumerable<TechnologyPrototype> EnumerateDatabaseTechnologies(TechnologyDatabaseComponent component)
    {
        if (!component.WeeklyModeOnly)
        {
            foreach (var tech in PrototypeManager.EnumeratePrototypes<TechnologyPrototype>())
                yield return tech;

            yield break;
        }

        foreach (var id in component.WeeklyAllowedTechnologies.Distinct())
        {
            if (PrototypeManager.TryIndex<TechnologyPrototype>(id, out var tech))
                yield return tech;
        }
    }

    public FormattedMessage GetWeeklyTechnologyDescription(
        WeeklyTechnologyData technology,
        bool includeCost = true,
        bool includeTier = true,
        TechDisciplinePrototype? disciplinePrototype = null)
    {
        var description = new FormattedMessage();
        if (includeTier)
        {
            disciplinePrototype ??= PrototypeManager.Index<TechDisciplinePrototype>(technology.Branch);
            description.AddMarkupOrThrow(Loc.GetString("research-console-tier-discipline-info",
                ("tier", technology.Tier), ("color", disciplinePrototype.Color), ("discipline", Loc.GetString(disciplinePrototype.Name))));
            description.PushNewline();
        }

        if (includeCost)
        {
            description.AddMarkupOrThrow(Loc.GetString("research-console-cost", ("amount", technology.Cost)));
            description.PushNewline();
        }

        description.AddMarkupOrThrow(Loc.GetString("research-console-unlocks-list-start"));
        foreach (var recipe in technology.RecipeIds)
        {
            description.PushNewline();
            var recipeName = PrototypeManager.TryIndex<LatheRecipePrototype>(recipe, out var recipeProto)
                ? _lathe.GetRecipeName(recipeProto)
                : recipe;
            description.AddMarkupOrThrow(Loc.GetString("research-console-unlocks-list-entry",
                ("name", recipeName)));
        }

        return description;
    }
}
