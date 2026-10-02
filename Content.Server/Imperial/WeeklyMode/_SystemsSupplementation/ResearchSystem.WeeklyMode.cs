using System.Linq;
using Content.Server.Research.Components;
using Content.Shared.Database;
using Content.Shared.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    public void SetWeeklyModeOverlay(
        EntityUid uid,
        bool enabled,
        IReadOnlyList<WeeklyTechnologyData> weeklyTechnologies,
        bool clearUnlocked,
        TechnologyDatabaseComponent? databaseComponent = null)
    {
        if (!Resolve(uid, ref databaseComponent, false))
            return;

        var previousWeeklyTechnologies = databaseComponent.WeeklyTechnologies;
        databaseComponent.WeeklyModeOnly = enabled;
        databaseComponent.WeeklyTechnologies = enabled ? weeklyTechnologies.ToList() : new List<WeeklyTechnologyData>();
        databaseComponent.WeeklyAllowedTechnologies = enabled
            ? weeklyTechnologies
                .Where(technology => PrototypeManager.HasIndex<TechnologyPrototype>(technology.TechnologyId))
                .Select(technology => new ProtoId<TechnologyPrototype>(technology.TechnologyId))
                .ToList()
            : new List<ProtoId<TechnologyPrototype>>();

        var allowed = weeklyTechnologies.Select(technology => technology.TechnologyId).ToHashSet(StringComparer.Ordinal);
        databaseComponent.WeeklyUnlockedTechnologies.RemoveAll(id => !allowed.Contains(id));

        if (!enabled)
        {
            var normalUnlockedRecipes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unlocked in databaseComponent.UnlockedTechnologies)
            {
                if (!PrototypeManager.TryIndex<TechnologyPrototype>(unlocked, out var technology))
                    continue;

                foreach (var recipe in technology.RecipeUnlocks)
                    normalUnlockedRecipes.Add(recipe);
            }

            foreach (var weeklyRecipe in previousWeeklyTechnologies.SelectMany(technology => technology.RecipeIds).Distinct(StringComparer.Ordinal))
            {
                if (!normalUnlockedRecipes.Contains(weeklyRecipe))
                    databaseComponent.UnlockedRecipes.Remove(weeklyRecipe);
            }

            databaseComponent.WeeklyUnlockedTechnologies.Clear();
        }
        else if (clearUnlocked)
        {
            var allowedPrototypes = databaseComponent.WeeklyAllowedTechnologies.ToHashSet();
            databaseComponent.UnlockedTechnologies.RemoveAll(id => !allowedPrototypes.Contains(id));

            var weeklyRecipesToKeep = databaseComponent.WeeklyTechnologies
                .Where(technology => databaseComponent.WeeklyUnlockedTechnologies.Contains(technology.TechnologyId))
                .SelectMany(technology => technology.RecipeIds)
                .ToHashSet(StringComparer.Ordinal);
            databaseComponent.UnlockedRecipes.RemoveAll(recipe => !weeklyRecipesToKeep.Contains(recipe));

            foreach (var recipe in weeklyRecipesToKeep)
            {
                if (!databaseComponent.UnlockedRecipes.Contains(recipe))
                    databaseComponent.UnlockedRecipes.Add(recipe);
            }
        }

        UpdateTechnologyCards(uid, databaseComponent);
    }

    public bool UnlockWeeklyTechnology(EntityUid client,
        string technologyId,
        EntityUid user,
        ResearchClientComponent? component = null,
        TechnologyDatabaseComponent? clientDatabase = null)
    {
        if (!Resolve(client, ref component, ref clientDatabase, false))
            return false;

        if (!TryGetClientServer(client, out var serverEnt, out var serverComp, component))
            return false;

        if (!TryGetWeeklyTechnology(clientDatabase, technologyId, out var technology))
            return false;

        if (!IsWeeklyTechnologyAvailable(clientDatabase, technology))
            return false;

        if (technology.Cost > serverComp.Points)
            return false;

        AddWeeklyTechnology(serverEnt.Value, technology);
        TrySetWeeklyMainDiscipline(technology, serverEnt.Value);
        ModifyServerPoints(serverEnt.Value, -technology.Cost);
        UpdateTechnologyCards(serverEnt.Value);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} unlocked weekly technology {technology.TechnologyId} (branch: {technology.Branch}, tier: {technology.Tier}) at {ToPrettyString(client)}, for server {ToPrettyString(serverEnt.Value)}.");
        return true;
    }

    public void AddWeeklyTechnology(EntityUid uid, WeeklyTechnologyData technology, TechnologyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.WeeklyUnlockedTechnologies.Contains(technology.TechnologyId))
            return;

        component.WeeklyUnlockedTechnologies.Add(technology.TechnologyId);
        var addedRecipes = new List<string>();
        foreach (var unlock in technology.RecipeIds)
        {
            if (component.UnlockedRecipes.Contains(unlock))
                continue;

            component.UnlockedRecipes.Add(unlock);
            addedRecipes.Add(unlock);
        }

        Dirty(uid, component);

        var ev = new TechnologyDatabaseModifiedEvent(addedRecipes);
        RaiseLocalEvent(uid, ref ev);
    }

    public bool TryGetWeeklyTechnology(TechnologyDatabaseComponent database, string technologyId, out WeeklyTechnologyData technology)
    {
        foreach (var candidate in database.WeeklyTechnologies)
        {
            if (!string.Equals(candidate.TechnologyId, technologyId, StringComparison.Ordinal))
                continue;

            technology = candidate;
            return true;
        }

        technology = default;
        return false;
    }

    public void TrySetWeeklyMainDiscipline(WeeklyTechnologyData technology, EntityUid uid, TechnologyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var discipline = PrototypeManager.Index<TechDisciplinePrototype>(technology.Branch);
        if (technology.Tier < discipline.LockoutTier)
            return;

        component.MainDiscipline = technology.Branch;
        Dirty(uid, component);

        var ev = new TechnologyDatabaseModifiedEvent();
        RaiseLocalEvent(uid, ref ev);
    }

    public void RefreshResearchConsoles()
    {
        var query = EntityQueryEnumerator<ResearchConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!_uiSystem.IsUiOpen(uid, ResearchConsoleUiKey.Key))
                continue;

            SyncClientWithServer(uid);
            UpdateConsoleInterface(uid, component);
        }
    }
}
