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
    public bool TryAddRecipe(
        string setId,
        string recipeId,
        string resultPrototype,
        int resultAmount,
        double productionTimeSeconds,
        string latheTargets,
        IReadOnlyList<string> materialSpecs,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalRecipes = CloneRecipes(recipes);

        recipeId = recipeId.Trim();
        if (recipes.Recipes.Any(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal)))
        {
            message = $"Weekly recipe '{recipeId}' already exists in set '{set.SetId}'.";
            return false;
        }

        if (!TryBuildWeeklyRecipeDefinition(recipeId, resultPrototype, resultAmount, productionTimeSeconds, latheTargets, materialSpecs, out var recipe, out message))
            return false;

        recipes.Recipes.Add(recipe);
        SortRecipes(recipes);
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Added weekly recipe '{recipe.Id}' to set '{set.SetId}'.";
        return true;
    }

    public bool TryUpdateRecipe(string setId, string recipeId, string field, IReadOnlyList<string> args, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalRecipes = CloneRecipes(recipes);
        var recipe = recipes.Recipes.FirstOrDefault(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal));
        if (recipe == null)
        {
            message = $"Weekly set '{set.SetId}' has no recipe '{recipeId}'.";
            return false;
        }

        if (IsWeeklyRecipeInUse(recipe.Id))
        {
            message = $"Weekly recipe '{recipe.Id}' is currently queued or producing. Update refused to preserve material refunds and output consistency.";
            return false;
        }

        switch (field.Trim().ToLowerInvariant())
        {
            case "result":
                if (args.Count != 2 ||
                    !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                {
                    message = "Usage: wm.recipe.update <setId> <recipeId> result <resultPrototype> <amount>";
                    return false;
                }

                recipe.ResultPrototype = args[0].Trim();
                recipe.ResultAmount = amount;
                break;
            case "time":
                if (args.Count != 1 ||
                    !double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    message = "Usage: wm.recipe.update <setId> <recipeId> time <seconds>";
                    return false;
                }

                recipe.ProductionTimeSeconds = seconds;
                break;
            case "targets":
                if (args.Count != 1 || !TryNormalizeLatheTargets(args[0], out var targets, out message))
                    return false;

                recipe.LatheTargets = targets;
                break;
            case "materials":
                if (args.Count < 1 || !TryParseRecipeMaterials(args, out var materials, out message))
                    return false;

                recipe.Materials = materials;
                break;
            default:
                message = "Recipe update field must be one of: result, time, targets, materials.";
                return false;
        }

        SortRecipes(recipes);
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Updated weekly recipe '{recipe.Id}' in set '{set.SetId}'.";
        return true;
    }

    public bool TryRemoveRecipe(string setId, string recipeId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalRecipes = CloneRecipes(recipes);
        var recipe = recipes.Recipes.FirstOrDefault(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal));
        if (recipe == null)
        {
            message = $"Weekly set '{set.SetId}' had no recipe '{recipeId}'.";
            return false;
        }

        if (IsWeeklyRecipeInUse(recipe.Id))
        {
            message = $"Weekly recipe '{recipe.Id}' is currently queued or producing. Remove refused.";
            return false;
        }

        var linkedTechnologies = set.WeeklyTechnologies
            .Where(technology => technology.RecipeIds.Contains(recipe.Id, StringComparer.Ordinal))
            .Select(technology => technology.TechnologyId)
            .ToList();
        if (linkedTechnologies.Count > 0)
        {
            message = $"Weekly recipe '{recipe.Id}' is linked to technologies and was not removed. Unlink it first: {FormatList(linkedTechnologies)}.";
            return false;
        }

        recipes.Recipes.Remove(recipe);
        SortRecipes(recipes);
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Removed weekly recipe '{recipeId}' from set '{set.SetId}'.";
        return true;
    }

    public bool TryClearRecipes(string setId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var inUse = recipes.Recipes
            .Where(recipe => IsWeeklyRecipeInUse(recipe.Id))
            .Select(recipe => recipe.Id)
            .ToList();
        if (inUse.Count > 0)
        {
            message = $"Refusing to clear weekly recipes currently queued or producing: {FormatList(inUse)}.";
            return false;
        }

        var originalRecipes = CloneRecipes(recipes);
        var removed = recipes.Recipes.Count;
        var linkedRecipes = recipes.Recipes
            .Where(recipe => set.WeeklyTechnologies.Any(technology => technology.RecipeIds.Contains(recipe.Id, StringComparer.Ordinal)))
            .Select(recipe => recipe.Id)
            .ToList();
        if (linkedRecipes.Count > 0)
        {
            message = $"Refusing to clear weekly recipes still linked to technologies: {FormatList(linkedRecipes)}.";
            return false;
        }

        recipes.Recipes.Clear();

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Cleared {removed} weekly recipes from set '{set.SetId}'.";
        return true;
    }

    public bool TryAddRecipeTarget(string setId, string recipeId, string target, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalRecipes = CloneRecipes(recipes);
        var recipe = recipes.Recipes.FirstOrDefault(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal));
        if (recipe == null)
        {
            message = $"Weekly set '{set.SetId}' has no recipe '{recipeId}'.";
            return false;
        }

        if (IsWeeklyRecipeInUse(recipe.Id))
        {
            message = $"Weekly recipe '{recipe.Id}' is currently queued or producing. Target update refused.";
            return false;
        }

        if (!TryNormalizeLatheTarget(target, out var normalizedTarget, out message))
            return false;

        if (!recipe.LatheTargets.Contains(normalizedTarget, StringComparer.Ordinal))
            recipe.LatheTargets.Add(normalizedTarget);
        recipe.LatheTargets.Sort(StringComparer.Ordinal);
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Added target '{normalizedTarget}' to weekly recipe '{recipe.Id}'.";
        return true;
    }

    public bool TryRemoveRecipeTarget(string setId, string recipeId, string target, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalRecipes = CloneRecipes(recipes);
        var recipe = recipes.Recipes.FirstOrDefault(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal));
        if (recipe == null)
        {
            message = $"Weekly set '{set.SetId}' has no recipe '{recipeId}'.";
            return false;
        }

        if (IsWeeklyRecipeInUse(recipe.Id))
        {
            message = $"Weekly recipe '{recipe.Id}' is currently queued or producing. Target update refused.";
            return false;
        }

        if (!TryNormalizeLatheTarget(target, out var normalizedTarget, out message))
            return false;

        recipe.LatheTargets.RemoveAll(existing => string.Equals(existing, normalizedTarget, StringComparison.Ordinal));
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitRecipesChange(set, originalRecipes, recipes, WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Removed target '{normalizedTarget}' from weekly recipe '{recipe.Id}'.";
        return true;
    }

    public bool TryLinkRecipe(string setId, string recipeId, string technologyId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var technology = set.WeeklyTechnologies.FirstOrDefault(entry => string.Equals(entry.TechnologyId, technologyId, StringComparison.Ordinal));
        if (technology == null)
        {
            message = $"Weekly set '{set.SetId}' has no technology '{technologyId}'.";
            return false;
        }

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        if (!recipes.Recipes.Any(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal)))
        {
            message = $"Weekly set '{set.SetId}' has no recipe '{recipeId}'.";
            return false;
        }

        if (technology.RecipeIds.Contains(recipeId, StringComparer.Ordinal))
        {
            message = $"Weekly recipe '{recipeId}' is already linked to technology '{technologyId}'.";
            return false;
        }

        var originalSet = CloneSet(set);
        var originalRecipes = CloneRecipes(recipes);
        technology.RecipeIds.Add(recipeId);
        technology.RecipeIds.Sort(StringComparer.Ordinal);
        SyncRecipeTechnologyLinks(set, recipes);

        if (!TryCommitConfigAndRecipesChange(originalSet, set, originalRecipes, recipes, WeeklyLiveConfigChange.Research | WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = $"Linked weekly recipe '{recipeId}' to technology '{technologyId}'.";
        return true;
    }

    public bool TryUnlinkRecipe(string setId, string recipeId, string technologyId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (IsWeeklyRecipeInUse(recipeId))
        {
            message = $"Weekly recipe '{recipeId}' is currently queued or producing. Unlink refused.";
            return false;
        }

        var technology = set.WeeklyTechnologies.FirstOrDefault(entry => string.Equals(entry.TechnologyId, technologyId, StringComparison.Ordinal));
        if (technology == null)
        {
            message = $"Weekly set '{set.SetId}' has no technology '{technologyId}'.";
            return false;
        }

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        var originalSet = CloneSet(set);
        var originalRecipes = CloneRecipes(recipes);
        var removed = technology.RecipeIds.RemoveAll(id => string.Equals(id, recipeId, StringComparison.Ordinal));
        SyncRecipeTechnologyLinks(set, recipes);

        if (removed > 0 && !TryCommitConfigAndRecipesChange(originalSet, set, originalRecipes, recipes, WeeklyLiveConfigChange.Research | WeeklyLiveConfigChange.Recipes, out message))
            return false;

        message = removed == 0
            ? $"Weekly recipe '{recipeId}' was not linked to technology '{technologyId}'."
            : $"Unlinked weekly recipe '{recipeId}' from technology '{technologyId}'.";
        return true;
    }

    public string ListRecipes(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        SyncRecipeTechnologyLinks(set, recipes);
        if (recipes.Recipes.Count == 0)
            return $"Weekly set '{set.SetId}' has no campaign recipes.";

        var lines = new List<string> { $"Weekly recipes for '{set.SetId}':" };
        foreach (var recipe in recipes.Recipes.OrderBy(recipe => recipe.Id, StringComparer.Ordinal))
        {
            lines.Add($"- {recipe.Id}: result={recipe.ResultPrototype}x{recipe.ResultAmount} time={recipe.ProductionTimeSeconds.ToString(CultureInfo.InvariantCulture)}s targets=[{FormatList(recipe.LatheTargets)}] materials=[{FormatDictionary(recipe.Materials)}] technologies=[{FormatList(recipe.TechnologyIds)}]");
        }

        return string.Join('\n', lines);
    }

    public string ShowRecipe(string setId, string recipeId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        SyncRecipeTechnologyLinks(set, recipes);
        var recipe = recipes.Recipes.FirstOrDefault(recipe => string.Equals(recipe.Id, recipeId, StringComparison.Ordinal));
        if (recipe == null)
            return $"Weekly set '{set.SetId}' has no recipe '{recipeId}'.";

        return string.Join('\n', new[]
        {
            $"Weekly recipe '{recipe.Id}' in set '{set.SetId}':",
            $"- resultPrototype: {recipe.ResultPrototype}",
            $"- resultAmount: {recipe.ResultAmount}",
            $"- productionTimeSeconds: {recipe.ProductionTimeSeconds.ToString(CultureInfo.InvariantCulture)}",
            $"- latheTargets: {FormatList(recipe.LatheTargets)}",
            $"- materials: {FormatDictionary(recipe.Materials)}",
            $"- technologyIds: {FormatList(recipe.TechnologyIds)}",
        });
    }

    public string ValidateRecipes(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var recipes = _store.LoadRecipesOrDefault(set.SetId);
        SyncRecipeTechnologyLinks(set, recipes);
        var errors = ValidateWeeklyRecipes(set, recipes).ToList();
        errors.AddRange(ValidateWeeklyTechnologies(set, recipes));
        return errors.Count == 0
            ? $"Weekly recipes for '{set.SetId}' are valid."
            : $"Weekly recipes for '{set.SetId}' are invalid:\n- {string.Join("\n- ", errors)}";
    }

    public string ShowConfig(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var lines = new List<string>
        {
            $"Weekly config for '{set.SetId}':",
            $"- schemaVersion: {set.SchemaVersion}",
            $"- displayName: {set.DisplayName}",
            $"- baseMapPrototype: {set.BaseMapPrototype}",
            $"- baseMapPath: {set.BaseMapPath}",
            $"- currentSnapshot: {set.CurrentSnapshot ?? "<base>"}",
            $"- autosave: interval={set.AutosaveMinutes}m warning={set.AutosaveWarningMinutes}m retain={set.RetainAutosaves}",
            $"- disabledRoles: {FormatList(set.DefaultDisabledJobs)}",
            $"- roleAliases: {FormatDictionary(set.DefaultRoleAliases)}",
            $"- roleLimits: {FormatDictionary(set.DefaultRoleLimits)}",
            $"- forcedRoleAssignments: {set.ForcedRoleAssignments.Count} entries",
            $"- persistAutonomousMobs: {set.PersistAutonomousMobs}",
            $"- persistPlayerControlledBorgs: {set.PersistPlayerControlledBorgs}",
            $"- excludedMobPrototypes: {FormatList(set.ExcludedMobPrototypes)}",
            $"- access: minPlaytimeHours={set.MinPlaytimeHours} discord={(string.IsNullOrWhiteSpace(set.DiscordChannel) ? "<none>" : set.DiscordChannel)}",
            $"- randomGameRulesEnabled: {set.RandomGameRulesEnabled}",
            $"- researchTree: {set.WeeklyTechnologies.Count} entries",
            $"- cargoCatalog: {set.WeeklyCargoProducts.Count} entries",
            $"- snapshots: {set.Snapshots.Count}",
            $"- active: {IsActiveSet(set.SetId)}",
        };

        return string.Join('\n', lines);
    }

    public string ValidateConfig(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        if (TryValidateSetConfig(set, out var errors))
            return $"Weekly set '{set.SetId}' config is valid.";

        return $"Weekly set '{set.SetId}' config is invalid:\n- {string.Join("\n- ", errors)}";
    }

    public string ExportConfig(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        return _store.ExportSetJson(set);
    }
}
