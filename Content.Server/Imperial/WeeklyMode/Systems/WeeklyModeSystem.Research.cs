using Content.Server.WeeklyMode.Components;
using Content.Shared.Imperial.ICCVar;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Cargo.Systems;
using Content.Server.Chemistry.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Light.EntitySystems;
using Content.Server.Maps;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Research.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.CartridgeLoader;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Containers;
using Content.Shared.Containers.ItemSlots;
using Content.Server.WeeklyMode.Storage;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Light.Components;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Materials;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.VendingMachines;
using Content.Shared.WeeklyMode;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server.WeeklyMode.Systems;

public sealed partial class WeeklyModeSystem
{
    public bool TryAddTechnology(
        string setId,
        string branch,
        string technologyId,
        int cost,
        int tier,
        IReadOnlyList<string> recipeIds,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        if (!TryNormalizeTechnologyBranch(branch, out branch, out message))
            return false;

        technologyId = technologyId.Trim();
        if (!WeeklyModeStore.IsSafeId(technologyId))
        {
            message = "technologyId must contain only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (set.WeeklyTechnologies.Any(entry => string.Equals(entry.TechnologyId, technologyId, StringComparison.Ordinal)))
        {
            message = $"Technology '{technologyId}' already exists in weekly set '{set.SetId}'.";
            return false;
        }

        if (cost < 0)
        {
            message = "Technology cost may not be negative.";
            return false;
        }

        if (tier is < 1 or > 3)
        {
            message = "Technology tier must be 1, 2 or 3.";
            return false;
        }

        var recipeConfig = _store.LoadRecipesOrDefault(set.SetId);
        var weeklyRecipeIds = recipeConfig.Recipes.Select(recipe => recipe.Id).ToHashSet(StringComparer.Ordinal);
        var recipes = new List<string>();
        foreach (var rawRecipe in recipeIds)
        {
            var recipeId = rawRecipe.Trim();
            if (!_prototype.TryIndex<LatheRecipePrototype>(recipeId, out _) &&
                !weeklyRecipeIds.Contains(recipeId))
            {
                message = $"Unknown lathe or weekly recipe '{recipeId}'. No technology was changed.";
                return false;
            }

            if (!recipes.Contains(recipeId, StringComparer.Ordinal))
                recipes.Add(recipeId);
        }

        set.WeeklyTechnologies.Add(new WeeklyTechnologyEntry
        {
            TechnologyId = technologyId,
            Branch = branch,
            Cost = cost,
            Tier = tier,
            RecipeIds = recipes,
        });

        set.WeeklyTechnologies.Sort((a, b) => string.Compare(a.TechnologyId, b.TechnologyId, StringComparison.Ordinal));

        if (!TryCommitResearchConfigChange(originalSet, set, out message))
            return false;

        message = $"Added weekly technology '{technologyId}' to branch '{branch}' in set '{set.SetId}'.";
        return true;
    }

    public bool TryUpdateTechnology(
        string setId,
        string technologyId,
        string branch,
        int cost,
        int tier,
        IReadOnlyList<string> recipeIds,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        if (!TryNormalizeTechnologyBranch(branch, out branch, out message))
            return false;

        technologyId = technologyId.Trim();
        var entry = set.WeeklyTechnologies.FirstOrDefault(entry =>
            string.Equals(entry.TechnologyId, technologyId, StringComparison.Ordinal));
        if (entry == null)
        {
            message = $"Weekly set '{set.SetId}' has no technology '{technologyId}'.";
            return false;
        }

        if (cost < 0)
        {
            message = "Technology cost may not be negative.";
            return false;
        }

        if (tier is < 1 or > 3)
        {
            message = "Technology tier must be 1, 2 or 3.";
            return false;
        }

        var recipeConfig = _store.LoadRecipesOrDefault(set.SetId);
        var weeklyRecipeIds = recipeConfig.Recipes.Select(recipe => recipe.Id).ToHashSet(StringComparer.Ordinal);
        var recipes = new List<string>();
        foreach (var rawRecipe in recipeIds)
        {
            var recipeId = rawRecipe.Trim();
            if (!_prototype.TryIndex<LatheRecipePrototype>(recipeId, out _) &&
                !weeklyRecipeIds.Contains(recipeId))
            {
                message = $"Unknown lathe or weekly recipe '{recipeId}'. No technology was changed.";
                return false;
            }

            if (!recipes.Contains(recipeId, StringComparer.Ordinal))
                recipes.Add(recipeId);
        }

        if (HasPurchasedWeeklyTechnology(set.SetId, technologyId))
        {
            var removedRecipes = entry.RecipeIds
                .Where(recipe => !recipes.Contains(recipe, StringComparer.Ordinal))
                .ToList();
            if (removedRecipes.Count > 0)
            {
                message = $"Weekly technology '{technologyId}' is already purchased. Refusing to remove unlocked recipes without an explicit remove --force. Removed recipes would be: {FormatList(removedRecipes)}.";
                return false;
            }
        }

        entry.Branch = branch;
        entry.Cost = cost;
        entry.Tier = tier;
        entry.RecipeIds = recipes;
        set.WeeklyTechnologies.Sort((a, b) => string.Compare(a.TechnologyId, b.TechnologyId, StringComparison.Ordinal));

        if (!TryCommitResearchConfigChange(originalSet, set, out message))
            return false;

        message = $"Updated weekly technology '{technologyId}' in set '{set.SetId}'.";
        return true;
    }

    public bool TryRemoveTechnology(string setId, string branch, string technologyId, bool force, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        if (!TryNormalizeTechnologyBranch(branch, out branch, out message))
            return false;

        if (!force && HasPurchasedWeeklyTechnology(set.SetId, technologyId))
        {
            message = $"Weekly technology '{technologyId}' is already purchased in the active campaign. Use '--force' to remove it and explicitly drop recipes that only come from that weekly technology.";
            return false;
        }

        var removed = set.WeeklyTechnologies.RemoveAll(entry =>
            string.Equals(entry.Branch, branch, StringComparison.Ordinal) &&
            string.Equals(entry.TechnologyId, technologyId, StringComparison.Ordinal));

        if (removed > 0 && !TryCommitResearchConfigChange(originalSet, set, out message))
            return false;

        message = removed == 0
            ? $"Weekly set '{set.SetId}' had no technology '{technologyId}' in branch '{branch}'."
            : $"Removed technology '{technologyId}' from branch '{branch}' in weekly set '{set.SetId}'.";
        return true;
    }

    public string ListTechnologies(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        if (set.WeeklyTechnologies.Count == 0)
            return $"Weekly set '{set.SetId}' has an empty research tree.";

        var lines = new List<string> { $"Weekly research tree for '{set.SetId}':" };
        foreach (var entry in set.WeeklyTechnologies
                     .OrderBy(x => x.Branch, StringComparer.Ordinal)
                     .ThenBy(x => x.TechnologyId, StringComparer.Ordinal))
        {
            lines.Add($"- {entry.Branch}: {entry.TechnologyId} cost={entry.Cost} tier={entry.Tier} recipes=[{FormatList(entry.RecipeIds)}]");
        }

        return string.Join('\n', lines);
    }

    public bool TryClearTechnologies(string setId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);
        var purchased = GetPurchasedWeeklyTechnologies(set.SetId);
        var removedPurchased = set.WeeklyTechnologies
            .Where(entry => purchased.Contains(entry.TechnologyId))
            .Select(entry => entry.TechnologyId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (removedPurchased.Count > 0)
        {
            message = $"Refusing to clear purchased weekly technologies from active set '{set.SetId}': {FormatList(removedPurchased)}.";
            return false;
        }

        var removed = set.WeeklyTechnologies.Count;
        set.WeeklyTechnologies.Clear();

        if (!TryCommitResearchConfigChange(originalSet, set, out message))
            return false;

        message = $"Cleared {removed} weekly technologies from set '{set.SetId}'.";
        return true;
    }

    public bool TryClearTechnologyBranch(string setId, string branch, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!TryNormalizeTechnologyBranch(branch, out branch, out message))
            return false;

        var originalSet = CloneSet(set);
        var purchased = GetPurchasedWeeklyTechnologies(set.SetId);
        var removedPurchased = set.WeeklyTechnologies
            .Where(entry =>
                string.Equals(entry.Branch, branch, StringComparison.Ordinal) &&
                purchased.Contains(entry.TechnologyId))
            .Select(entry => entry.TechnologyId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (removedPurchased.Count > 0)
        {
            message = $"Refusing to clear purchased weekly technologies from branch '{branch}' in active set '{set.SetId}': {FormatList(removedPurchased)}.";
            return false;
        }

        var removed = set.WeeklyTechnologies.RemoveAll(entry => string.Equals(entry.Branch, branch, StringComparison.Ordinal));

        if (removed > 0 && !TryCommitResearchConfigChange(originalSet, set, out message))
            return false;

        message = $"Cleared {removed} weekly technologies from branch '{branch}' in set '{set.SetId}'.";
        return true;
    }

    public string ValidateTechnologies(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var errors = ValidateWeeklyTechnologies(set, _store.LoadRecipesOrDefault(set.SetId)).ToList();
        return errors.Count == 0
            ? $"Weekly research tree for '{set.SetId}' is valid."
            : $"Weekly research tree for '{set.SetId}' is invalid:\n- {string.Join("\n- ", errors)}";
    }
}
